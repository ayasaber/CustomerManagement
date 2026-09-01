using System.Security.Claims;
using CustomerManagement.Api.Contracts.TicketTasks;
using CustomerManagement.Api.Domain.Dashboard;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Dashboard;

public static class TicketTasksEndpoints
{
    public static IEndpointRouteBuilder MapTicketTasksEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ticket-tasks").WithTags("TicketTasks");

        group.MapGet("", ListAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketTasksRead)
            .WithName("ListTicketTasks")
            .WithSummary("List ticket-linked tasks");

        group.MapPost("", CreateAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketTasksWrite)
            .WithName("CreateTicketTask")
            .WithSummary("Create a ticket-linked task");

        group.MapPut("/{taskId:guid}", UpdateAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketTasksWrite)
            .WithName("UpdateTicketTask")
            .WithSummary("Update a ticket-linked task");

        group.MapPut("/{taskId:guid}/complete", CompleteAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketTasksComplete)
            .WithName("CompleteTicketTask")
            .WithSummary("Mark a ticket-linked task as done");

        return app;
    }

    private static async Task<IResult> ListAsync(
        Guid? ticketId,
        string? status,
        bool? assignedToMeOnly,
        int? page,
        int? pageSize,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var resolvedPage = page.GetValueOrDefault(1);
        var resolvedPageSize = pageSize.GetValueOrDefault(20);

        if (resolvedPage < 1)
        {
            errors["page"] = ["Page must be greater than or equal to 1."];
        }

        if (resolvedPageSize < 1 || resolvedPageSize > 200)
        {
            errors["pageSize"] = ["PageSize must be between 1 and 200."];
        }

        var parsedStatus = ParseStatus(status);
        if (!string.IsNullOrWhiteSpace(status) && !parsedStatus.HasValue)
        {
            errors["status"] = ["Status filter is invalid."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var actorUserId = ResolveActorUserId(httpContext.User);
        if (!actorUserId.HasValue)
        {
            return Results.Forbid();
        }

        var isAdmin = IsAdmin(httpContext.User);
        var resolvedAssignedToMeOnly = assignedToMeOnly ?? !isAdmin;

        var query = dbContext.TicketTasks
            .AsNoTracking()
            .Include(task => task.Ticket)
            .Include(task => task.AssignedToUser)
            .AsQueryable();

        if (ticketId.HasValue)
        {
            var ticket = await dbContext.Tickets
                .AsNoTracking()
                .Where(row => row.Id == ticketId.Value)
                .Select(row => new { row.Id, row.AssignedToUserId })
                .FirstOrDefaultAsync(cancellationToken);

            if (ticket is null)
            {
                return Results.NotFound();
            }

            if (!CanViewTicket(isAdmin, actorUserId.Value, ticket.AssignedToUserId))
            {
                return Results.NotFound();
            }

            query = query.Where(task => task.TicketId == ticketId.Value);
        }

        if (!isAdmin || resolvedAssignedToMeOnly)
        {
            query = query.Where(task => task.AssignedToUserId == actorUserId.Value);
        }

        if (parsedStatus.HasValue)
        {
            query = query.Where(task => task.Status == parsedStatus.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(task => task.Status)
            .ThenBy(task => task.DueAtUtc)
            .ThenBy(task => task.Id)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .Select(task => MapToResponse(task))
            .ToListAsync(cancellationToken);

        return Results.Ok(new TicketTaskListResponse(totalCount, items));
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateTicketTaskRequest request,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actorUserId = ResolveActorUserId(httpContext.User);
        if (!actorUserId.HasValue)
        {
            return Results.Forbid();
        }

        var resolvedDueAtUtc = NormalizeUtc(request.DueAtUtc);
        var assigneeUserId = request.AssignedToUserId ?? actorUserId.Value;

        var errors = ValidateCreate(request.TicketId, request.Description, resolvedDueAtUtc, assigneeUserId);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .Where(row => row.Id == request.TicketId)
            .Select(row => new { row.Id, row.AssignedToUserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (ticket is null)
        {
            return Results.NotFound();
        }

        var isAdmin = IsAdmin(httpContext.User);
        if (!CanViewTicket(isAdmin, actorUserId.Value, ticket.AssignedToUserId))
        {
            return Results.Forbid();
        }

        var assignee = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == assigneeUserId && user.IsActive, cancellationToken);

        if (assignee is null)
        {
            return Results.NotFound();
        }

        var hasAgentRole = await IsAgentOrAdminRoleAsync(assignee.Id, dbContext, cancellationToken);
        if (!hasAgentRole)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedToUserId"] = ["Assignee must have agent or admin role."]
            });
        }

        var now = DateTime.UtcNow;
        var task = new TicketTask
        {
            Id = Guid.NewGuid(),
            TicketId = request.TicketId,
            CreatedByUserId = actorUserId.Value,
            AssignedToUserId = assigneeUserId,
            Description = request.Description.Trim(),
            DueAtUtc = resolvedDueAtUtc,
            Status = TicketTaskStatus.Open,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CompletedAtUtc = null,
            CompletedByUserId = null
        };

        dbContext.TicketTasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);

        var created = await dbContext.TicketTasks
            .AsNoTracking()
            .Include(row => row.Ticket)
            .Include(row => row.AssignedToUser)
            .FirstAsync(row => row.Id == task.Id, cancellationToken);

        return Results.Created($"/api/ticket-tasks/{task.Id}", MapToResponse(created));
    }

    private static async Task<IResult> UpdateAsync(
        Guid taskId,
        [FromBody] UpdateTicketTaskRequest request,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actorUserId = ResolveActorUserId(httpContext.User);
        if (!actorUserId.HasValue)
        {
            return Results.Forbid();
        }

        var resolvedDueAtUtc = NormalizeUtc(request.DueAtUtc);
        var errors = ValidateUpdate(request.Description, resolvedDueAtUtc, request.AssignedToUserId, request.RowVersion);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var task = await dbContext.TicketTasks
            .Include(row => row.Ticket)
            .FirstOrDefaultAsync(row => row.Id == taskId, cancellationToken);

        if (task is null)
        {
            return Results.NotFound();
        }

        var isAdmin = IsAdmin(httpContext.User);
        if (!isAdmin && task.CreatedByUserId != actorUserId.Value && task.AssignedToUserId != actorUserId.Value)
        {
            return Results.Forbid();
        }

        var assignee = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == request.AssignedToUserId && user.IsActive, cancellationToken);

        if (assignee is null)
        {
            return Results.NotFound();
        }

        var hasAgentRole = await IsAgentOrAdminRoleAsync(assignee.Id, dbContext, cancellationToken);
        if (!hasAgentRole)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedToUserId"] = ["Assignee must have agent or admin role."]
            });
        }

        dbContext.Entry(task).Property(row => row.RowVersion).OriginalValue = request.RowVersion;

        task.Description = request.Description.Trim();
        task.DueAtUtc = resolvedDueAtUtc;
        task.AssignedToUserId = request.AssignedToUserId;
        task.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Concurrency conflict",
                Detail = "Ticket task was updated by another request. Refresh and retry with the latest RowVersion.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var updated = await dbContext.TicketTasks
            .AsNoTracking()
            .Include(row => row.Ticket)
            .Include(row => row.AssignedToUser)
            .FirstAsync(row => row.Id == task.Id, cancellationToken);

        return Results.Ok(MapToResponse(updated));
    }

    private static async Task<IResult> CompleteAsync(
        Guid taskId,
        [FromBody] CompleteTicketTaskRequest request,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.RowVersion is null || request.RowVersion.Length == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["RowVersion is required."]
            });
        }

        var actorUserId = ResolveActorUserId(httpContext.User);
        if (!actorUserId.HasValue)
        {
            return Results.Forbid();
        }

        var task = await dbContext.TicketTasks
            .Include(row => row.Ticket)
            .FirstOrDefaultAsync(row => row.Id == taskId, cancellationToken);

        if (task is null)
        {
            return Results.NotFound();
        }

        var isAdmin = IsAdmin(httpContext.User);
        if (!isAdmin && task.AssignedToUserId != actorUserId.Value)
        {
            return Results.Forbid();
        }

        if (task.Status == TicketTaskStatus.Done)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Invalid operation",
                Detail = "task_already_completed",
                Status = StatusCodes.Status409Conflict
            });
        }

        dbContext.Entry(task).Property(row => row.RowVersion).OriginalValue = request.RowVersion;

        var now = DateTime.UtcNow;
        task.Status = TicketTaskStatus.Done;
        task.CompletedAtUtc = now;
        task.CompletedByUserId = actorUserId.Value;
        task.UpdatedAtUtc = now;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Concurrency conflict",
                Detail = "Ticket task was updated by another request. Refresh and retry with the latest RowVersion.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var completed = await dbContext.TicketTasks
            .AsNoTracking()
            .Include(row => row.Ticket)
            .Include(row => row.AssignedToUser)
            .FirstAsync(row => row.Id == task.Id, cancellationToken);

        return Results.Ok(MapToResponse(completed));
    }

    private static TicketTaskResponse MapToResponse(TicketTask task)
    {
        return new TicketTaskResponse(
            task.Id,
            task.TicketId,
            FormatTicketNumber(task.TicketId),
            task.CreatedByUserId,
            task.AssignedToUserId,
            task.AssignedToUser.DisplayName,
            task.Description,
            task.DueAtUtc,
            NormalizeStatus(task.Status),
            task.CreatedAtUtc,
            task.UpdatedAtUtc,
            task.CompletedAtUtc,
            task.CompletedByUserId,
            task.RowVersion);
    }

    private static bool CanViewTicket(bool isAdmin, Guid actorUserId, Guid? ticketAssigneeUserId)
    {
        if (isAdmin)
        {
            return true;
        }

        return !ticketAssigneeUserId.HasValue || ticketAssigneeUserId == actorUserId;
    }

    private static async Task<bool> IsAgentOrAdminRoleAsync(
        Guid userId,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return await (
                from userRole in dbContext.UserRoles.AsNoTracking()
                join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userRole.UserId == userId
                select role.Name)
            .AnyAsync(roleName => roleName == AuthRoles.Agent || roleName == AuthRoles.Admin, cancellationToken);
    }

    private static Dictionary<string, string[]> ValidateCreate(Guid ticketId, string description, DateTime dueAtUtc, Guid assignedToUserId)
    {
        var errors = new Dictionary<string, string[]>();

        if (ticketId == Guid.Empty)
        {
            errors["ticketId"] = ["TicketId is required."];
        }

        if (assignedToUserId == Guid.Empty)
        {
            errors["assignedToUserId"] = ["AssignedToUserId is required."];
        }

        ValidateCommon(description, dueAtUtc, errors);
        return errors;
    }

    private static Dictionary<string, string[]> ValidateUpdate(string description, DateTime dueAtUtc, Guid assignedToUserId, byte[] rowVersion)
    {
        var errors = new Dictionary<string, string[]>();

        if (assignedToUserId == Guid.Empty)
        {
            errors["assignedToUserId"] = ["AssignedToUserId is required."];
        }

        if (rowVersion is null || rowVersion.Length == 0)
        {
            errors["rowVersion"] = ["RowVersion is required."];
        }

        ValidateCommon(description, dueAtUtc, errors);
        return errors;
    }

    private static void ValidateCommon(string description, DateTime dueAtUtc, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            errors["description"] = ["Description is required."];
        }
        else if (description.Trim().Length > 500)
        {
            errors["description"] = ["Description must be 500 characters or fewer."];
        }

        if (dueAtUtc == DateTime.MinValue || dueAtUtc == DateTime.MaxValue)
        {
            errors["dueAtUtc"] = ["DueAtUtc is invalid."];
        }
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        return value.ToUniversalTime();
    }

    private static string NormalizeStatus(TicketTaskStatus status)
    {
        return status == TicketTaskStatus.Done ? "done" : "open";
    }

    private static TicketTaskStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "open" => TicketTaskStatus.Open,
            "done" => TicketTaskStatus.Done,
            _ => null
        };
    }

    private static string FormatTicketNumber(Guid ticketId)
    {
        return $"TKT-{ticketId:N}"[..12].ToUpperInvariant();
    }

    private static Guid? ResolveActorUserId(ClaimsPrincipal actor)
    {
        var candidate = actor.FindFirstValue(ClaimTypes.NameIdentifier) ?? actor.FindFirstValue("sub");
        return Guid.TryParse(candidate, out var parsed) ? parsed : null;
    }

    private static bool IsAdmin(ClaimsPrincipal actor)
    {
        return actor.IsInRole(AuthRoles.Admin)
            || actor.Claims.Any(claim =>
                string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                && string.Equals(claim.Value, AuthRoles.Admin, StringComparison.OrdinalIgnoreCase));
    }
}
