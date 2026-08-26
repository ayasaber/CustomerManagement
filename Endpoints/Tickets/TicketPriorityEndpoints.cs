using CustomerManagement.Api.Contracts.Tickets;
using CustomerManagement.Api.Domain.Tickets;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Tickets;

public static class TicketPriorityEndpoints
{
    public static IEndpointRouteBuilder MapTicketPriorityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tickets/priorities").WithTags("Tickets.Priorities");

        group.MapGet("", ListPrioritiesAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketsRead)
            .WithName("ListTicketPriorities")
            .WithSummary("List ticket priorities");

        group.MapPost("", CreatePriorityAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketTaxonomyManage)
            .WithName("CreateTicketPriority")
            .WithSummary("Create a ticket priority");

        group.MapPut("/{priorityId:guid}", UpdatePriorityAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketTaxonomyManage)
            .WithName("UpdateTicketPriority")
            .WithSummary("Update a ticket priority");

        return app;
    }

    private static async Task<IResult> ListPrioritiesAsync(
        [FromQuery] bool? activeOnly,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.TicketPriorities.AsNoTracking().AsQueryable();
        if (activeOnly.GetValueOrDefault())
        {
            query = query.Where(priority => priority.IsActive);
        }

        var priorities = await query
            .OrderBy(priority => priority.SortOrder)
            .ThenBy(priority => priority.Name)
            .Select(priority => new TicketPriorityResponse(
                priority.Id,
                priority.Name,
                priority.SortOrder,
                priority.IsActive,
                priority.CreatedAtUtc,
                priority.UpdatedAtUtc,
                priority.RowVersion))
            .ToListAsync(cancellationToken);

        return Results.Ok(priorities);
    }

    private static async Task<IResult> CreatePriorityAsync(
        [FromBody] CreateTicketPriorityRequest request,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var normalizedName = request.Name.Trim();
        var duplicate = await dbContext.TicketPriorities
            .AsNoTracking()
            .AnyAsync(priority => priority.Name == normalizedName, cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { message = "Priority name already exists." });
        }

        var now = DateTime.UtcNow;
        var priority = new TicketPriority
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.TicketPriorities.Add(priority);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/tickets/priorities/{priority.Id}",
            new TicketPriorityResponse(
                priority.Id,
                priority.Name,
                priority.SortOrder,
                priority.IsActive,
                priority.CreatedAtUtc,
                priority.UpdatedAtUtc,
                priority.RowVersion));
    }

    private static async Task<IResult> UpdatePriorityAsync(
        Guid priorityId,
        [FromBody] UpdateTicketPriorityRequest request,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateUpdateRequest(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var priority = await dbContext.TicketPriorities
            .FirstOrDefaultAsync(row => row.Id == priorityId, cancellationToken);

        if (priority is null)
        {
            return Results.NotFound();
        }

        var normalizedName = request.Name.Trim();
        var duplicate = await dbContext.TicketPriorities
            .AsNoTracking()
            .AnyAsync(row => row.Id != priorityId && row.Name == normalizedName, cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { message = "Priority name already exists." });
        }

        priority.Name = normalizedName;
        priority.SortOrder = request.SortOrder;
        priority.IsActive = request.IsActive;
        priority.UpdatedAtUtc = DateTime.UtcNow;
        dbContext.Entry(priority).Property(row => row.RowVersion).OriginalValue = request.RowVersion;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Concurrency conflict",
                Detail = "Priority was updated by another request. Refresh and retry with the latest RowVersion.",
                Status = StatusCodes.Status409Conflict
            });
        }

        return Results.Ok(new TicketPriorityResponse(
            priority.Id,
            priority.Name,
            priority.SortOrder,
            priority.IsActive,
            priority.CreatedAtUtc,
            priority.UpdatedAtUtc,
            priority.RowVersion));
    }

    private static Dictionary<string, string[]> ValidateCreateRequest(CreateTicketPriorityRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (request.Name.Trim().Length > 100)
        {
            errors["name"] = ["Name must be 100 characters or fewer."];
        }

        if (request.SortOrder is < 0 or > 1000)
        {
            errors["sortOrder"] = ["SortOrder must be between 0 and 1000."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateUpdateRequest(UpdateTicketPriorityRequest request)
    {
        var errors = ValidateCreateRequest(new CreateTicketPriorityRequest(request.Name, request.SortOrder));

        if (request.RowVersion is null || request.RowVersion.Length == 0)
        {
            errors["rowVersion"] = ["RowVersion is required."];
        }

        return errors;
    }
}
