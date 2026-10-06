using System.Security.Claims;
using CustomerManagement.Api.Contracts.Tickets;
using CustomerManagement.Api.Domain.Customers;
using CustomerManagement.Api.Domain.Tickets;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Tickets;

public static class TicketEndpoints
{
    public static IEndpointRouteBuilder MapTicketEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tickets").WithTags("Tickets");

        group.MapPost("", CreateTicketAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketsWrite)
            .WithName("CreateTicket")
            .WithSummary("Create a new ticket");

        group.MapGet("", ListTicketsAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketsRead)
            .WithName("ListTickets")
            .WithSummary("List tickets with filters and pagination");

        group.MapGet("/{ticketId:guid}", GetTicketAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketsRead)
            .WithName("GetTicket")
            .WithSummary("Get ticket by id");

        group.MapPut("/{ticketId:guid}", UpdateTicketCoreAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketsWrite)
            .WithName("UpdateTicket")
            .WithSummary("Update ticket core fields");

        group.MapPut("/{ticketId:guid}/status", UpdateTicketStatusAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketsWrite)
            .WithName("UpdateTicketStatus")
            .WithSummary("Transition ticket status");

        group.MapPut("/{ticketId:guid}/assign", AssignTicketAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketsAssign)
            .WithName("AssignTicket")
            .WithSummary("Assign a ticket to a user");

        group.MapPut("/{ticketId:guid}/self-assign", SelfAssignTicketAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketsAssign)
            .WithName("SelfAssignTicket")
            .WithSummary("Assign a ticket to current user");

        group.MapPost("/{ticketId:guid}/escalate", EscalateTicketAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketsEscalate)
            .WithName("EscalateTicket")
            .WithSummary("Mark a ticket as escalated");

        group.MapPost("/{ticketId:guid}/reopen", ReopenTicketAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketsClose)
            .WithName("ReopenTicket")
            .WithSummary("Reopen a resolved or closed ticket");

        group.MapGet("/{ticketId:guid}/history", GetTicketHistoryAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketsRead)
            .WithName("GetTicketHistory")
            .WithSummary("Get ticket history entries");

        return app;
    }

    private static async Task<IResult> CreateTicketAsync(
        [FromBody] CreateTicketRequest request,
        CustomerManagementDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var resolvedCustomerId = await ResolveCreateCustomerIdAsync(request, httpContext.User, dbContext, cancellationToken);
        if (!resolvedCustomerId.HasValue)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["customerUserId"] = ["A valid linked customer user is required for ticket creation."]
            });
        }

        Guid priorityId;
        if (IsCustomer(httpContext.User))
        {
            // Customers never choose priority; the support team decides it after triage.
            var defaultPriorityId = await ResolveDefaultPriorityIdAsync(dbContext, cancellationToken);
            if (!defaultPriorityId.HasValue)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["priorityId"] = ["No active ticket priority is configured; contact an administrator."]
                });
            }

            priorityId = defaultPriorityId.Value;
        }
        else
        {
            priorityId = request.PriorityId.GetValueOrDefault();
        }

        var errors = await ValidateCreateTicketAsync(resolvedCustomerId.Value, priorityId, request, dbContext, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (ShouldApplyCustomerScope(httpContext.User))
        {
            var ownedCustomerIds = await ResolveOwnedCustomerIdsAsync(httpContext.User, dbContext, cancellationToken);
            if (!ownedCustomerIds.Contains(resolvedCustomerId.Value))
            {
                return Results.Forbid();
            }
        }

        var now = DateTime.UtcNow;
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            CustomerId = resolvedCustomerId.Value,
            CategoryId = request.CategoryId,
            PriorityId = priorityId,
            Subject = request.Subject.Trim(),
            Description = request.Description.Trim(),
            Status = TicketStatus.New,
            IsEscalated = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Tickets.Add(ticket);
        AddHistory(
            dbContext,
            ticket,
            "ticket.created",
            "status",
            null,
            ToApiStatus(ticket.Status),
            httpContext.User,
            now);

        await dbContext.SaveChangesAsync(cancellationToken);

        var created = await LoadTicketProjectionAsync(ticket.Id, dbContext, cancellationToken);
        return Results.Created($"/api/tickets/{ticket.Id}", created);
    }

    private static async Task<IResult> ListTicketsAsync(
        [FromQuery] Guid? customerId,
        [FromQuery] Guid? assignedToUserId,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? priorityId,
        [FromQuery] bool? escalatedOnly,
        [FromQuery] string? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var resolvedPage = page.GetValueOrDefault(1);
        var resolvedPageSize = pageSize.GetValueOrDefault(50);

        if (resolvedPage < 1)
        {
            errors["page"] = ["Page must be greater than or equal to 1."];
        }

        if (resolvedPageSize < 1 || resolvedPageSize > 200)
        {
            errors["pageSize"] = ["PageSize must be between 1 and 200."];
        }

        var hasStatusFilter = !string.IsNullOrWhiteSpace(status);
        var parsedStatus = ParseStatus(status);
        if (hasStatusFilter && !parsedStatus.HasValue)
        {
            errors["status"] = ["Status filter is invalid."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var query = dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.Category)
            .Include(ticket => ticket.Priority)
            .Include(ticket => ticket.AssignedToUser)
            .AsQueryable();

        if (ShouldApplyCustomerScope(httpContext.User))
        {
            var ownedCustomerIds = await ResolveOwnedCustomerIdsAsync(httpContext.User, dbContext, cancellationToken);
            if (ownedCustomerIds.Count == 0)
            {
                return Results.Ok(new TicketListResponse(resolvedPage, resolvedPageSize, 0, []));
            }

            query = query.Where(ticket => ownedCustomerIds.Contains(ticket.CustomerId));
        }

        if (customerId.HasValue)
        {
            query = query.Where(ticket => ticket.CustomerId == customerId.Value);
        }

        if (assignedToUserId.HasValue)
        {
            query = query.Where(ticket => ticket.AssignedToUserId == assignedToUserId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(ticket => ticket.CategoryId == categoryId.Value);
        }

        if (priorityId.HasValue)
        {
            query = query.Where(ticket => ticket.PriorityId == priorityId.Value);
        }

        if (escalatedOnly.GetValueOrDefault())
        {
            query = query.Where(ticket => ticket.IsEscalated);
        }

        if (parsedStatus.HasValue)
        {
            query = query.Where(ticket => ticket.Status == parsedStatus.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(ticket => ticket.CreatedAtUtc)
            .ThenBy(ticket => ticket.Id)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .Select(ticket => new TicketListItemResponse(
                ticket.Id,
                ticket.CustomerId,
                ticket.AssignedToUserId,
                ticket.AssignedToUser != null ? ticket.AssignedToUser.Email : null,
                ticket.CategoryId,
                ticket.Category.Name,
                ticket.PriorityId,
                ticket.Priority.Name,
                ticket.Subject,
                ToApiStatus(ticket.Status),
                ticket.IsEscalated,
                ticket.CreatedAtUtc,
                ticket.UpdatedAtUtc,
                ticket.RowVersion))
            .ToListAsync(cancellationToken);

        return Results.Ok(new TicketListResponse(resolvedPage, resolvedPageSize, totalCount, items));
    }

    private static async Task<IResult> GetTicketAsync(
        Guid ticketId,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var ticket = await LoadTicketProjectionAsync(ticketId, dbContext, cancellationToken);
        if (ticket is null)
        {
            return Results.NotFound();
        }

        if (ShouldApplyCustomerScope(httpContext.User))
        {
            var ownedCustomerIds = await ResolveOwnedCustomerIdsAsync(httpContext.User, dbContext, cancellationToken);
            if (!ownedCustomerIds.Contains(ticket.CustomerId))
            {
                return Results.NotFound();
            }
        }

        return Results.Ok(ticket);
    }

    private static async Task<IResult> UpdateTicketCoreAsync(
        Guid ticketId,
        [FromBody] UpdateTicketCoreRequest request,
        CustomerManagementDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var errors = await ValidateUpdateCoreAsync(request, dbContext, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(row => row.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return Results.NotFound();
        }

        if (ShouldApplyCustomerScope(httpContext.User))
        {
            var ownedCustomerIds = await ResolveOwnedCustomerIdsAsync(httpContext.User, dbContext, cancellationToken);
            if (!ownedCustomerIds.Contains(ticket.CustomerId))
            {
                return Results.NotFound();
            }
        }

        var now = DateTime.UtcNow;
        dbContext.Entry(ticket).Property(row => row.RowVersion).OriginalValue = request.RowVersion;

        if (ticket.CategoryId != request.CategoryId)
        {
            AddHistory(dbContext, ticket, "ticket.updated", "categoryId", ticket.CategoryId.ToString(), request.CategoryId.ToString(), httpContext.User, now);
            ticket.CategoryId = request.CategoryId;
        }

        if (ticket.PriorityId != request.PriorityId)
        {
            AddHistory(dbContext, ticket, "ticket.updated", "priorityId", ticket.PriorityId.ToString(), request.PriorityId.ToString(), httpContext.User, now);
            ticket.PriorityId = request.PriorityId;
        }

        var subject = request.Subject.Trim();
        if (!string.Equals(ticket.Subject, subject, StringComparison.Ordinal))
        {
            AddHistory(dbContext, ticket, "ticket.updated", "subject", ticket.Subject, subject, httpContext.User, now);
            ticket.Subject = subject;
        }

        var description = request.Description.Trim();
        if (!string.Equals(ticket.Description, description, StringComparison.Ordinal))
        {
            AddHistory(dbContext, ticket, "ticket.updated", "description", ticket.Description, description, httpContext.User, now);
            ticket.Description = description;
        }

        ticket.UpdatedAtUtc = now;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Concurrency conflict",
                Detail = "Ticket was updated by another request. Refresh and retry with the latest RowVersion.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var updated = await LoadTicketProjectionAsync(ticket.Id, dbContext, cancellationToken);
        return Results.Ok(updated);
    }

    private static async Task<IResult> UpdateTicketStatusAsync(
        Guid ticketId,
        [FromBody] UpdateTicketStatusRequest request,
        CustomerManagementDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateTransitionRequest(request.TargetStatus, request.RowVersion);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(row => row.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return Results.NotFound();
        }

        if (ShouldApplyCustomerScope(httpContext.User))
        {
            var ownedCustomerIds = await ResolveOwnedCustomerIdsAsync(httpContext.User, dbContext, cancellationToken);
            if (!ownedCustomerIds.Contains(ticket.CustomerId))
            {
                return Results.NotFound();
            }
        }

        var actorUserId = ResolveActorUserId(httpContext.User);
        var isAdmin = IsAdmin(httpContext.User);
        var isAgent = IsAgent(httpContext.User);
        if (!isAdmin && isAgent)
        {
            if (!actorUserId.HasValue)
            {
                return Results.Forbid();
            }
        }

        var targetStatus = ParseStatus(request.TargetStatus)!;
        if (!CanTransition(ticket.Status, targetStatus.Value))
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid operation",
                Detail = $"Cannot transition ticket from '{ToApiStatus(ticket.Status)}' to '{ToApiStatus(targetStatus.Value)}'.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (targetStatus.Value is TicketStatus.Resolved or TicketStatus.Closed)
        {
            // Customer callers are ownership-scoped above; internal close permission check applies to non-customer actors.
            if (!IsCustomer(httpContext.User))
            {
                var hasClosePermission = httpContext.User.Claims.Any(claim =>
                    string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(claim.Value, Permissions.TicketsClose, StringComparison.OrdinalIgnoreCase));

                if (!hasClosePermission)
                {
                    return Results.Forbid();
                }
            }
        }

        var now = DateTime.UtcNow;
        dbContext.Entry(ticket).Property(row => row.RowVersion).OriginalValue = request.RowVersion;

        if (!isAdmin && isAgent && actorUserId.HasValue && !ticket.AssignedToUserId.HasValue)
        {
            AddHistory(
                dbContext,
                ticket,
                "ticket.assigned",
                "assignedToUserId",
                null,
                actorUserId.Value.ToString(),
                httpContext.User,
                now);

            ticket.AssignedToUserId = actorUserId.Value;
        }

        AddHistory(
            dbContext,
            ticket,
            "ticket.status.changed",
            "status",
            ToApiStatus(ticket.Status),
            ToApiStatus(targetStatus.Value),
            httpContext.User,
            now);

        ticket.Status = targetStatus.Value;
        ticket.UpdatedAtUtc = now;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Concurrency conflict",
                Detail = "Ticket was updated by another request. Refresh and retry with the latest RowVersion.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var updated = await LoadTicketProjectionAsync(ticket.Id, dbContext, cancellationToken);
        return Results.Ok(updated);
    }

    private static async Task<IResult> AssignTicketAsync(
        Guid ticketId,
        [FromBody] AssignTicketRequest request,
        CustomerManagementDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var actorUserId = ResolveActorUserId(httpContext.User);
        var isAdmin = IsAdmin(httpContext.User);
        if (!isAdmin)
        {
            if (!actorUserId.HasValue)
            {
                return Results.Forbid();
            }

            if (request.AssigneeUserId != actorUserId.Value)
            {
                return Results.Forbid();
            }
        }

        var errors = ValidateAssignmentRequest(request.AssigneeUserId, request.RowVersion);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var assignee = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == request.AssigneeUserId && user.IsActive, cancellationToken);

        if (assignee is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assigneeUserId"] = ["Assignee user does not exist or is inactive."]
            });
        }

        var hasAgentRole = await (
                from userRole in dbContext.UserRoles.AsNoTracking()
                join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userRole.UserId == assignee.Id
                select role.Name)
            .AnyAsync(roleName => roleName == AuthRoles.Agent || roleName == AuthRoles.Admin, cancellationToken);

        if (!hasAgentRole)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assigneeUserId"] = ["Assignee must have agent or admin role."]
            });
        }

        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(row => row.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var now = DateTime.UtcNow;
        dbContext.Entry(ticket).Property(row => row.RowVersion).OriginalValue = request.RowVersion;
        var oldValue = ticket.AssignedToUserId?.ToString();
        var newValue = request.AssigneeUserId.ToString();

        if (!string.Equals(oldValue, newValue, StringComparison.OrdinalIgnoreCase))
        {
            AddHistory(dbContext, ticket, "ticket.assigned", "assignedToUserId", oldValue, newValue, httpContext.User, now);
        }

        ticket.AssignedToUserId = request.AssigneeUserId;
        ticket.UpdatedAtUtc = now;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Concurrency conflict",
                Detail = "Ticket was updated by another request. Refresh and retry with the latest RowVersion.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var updated = await LoadTicketProjectionAsync(ticket.Id, dbContext, cancellationToken);
        return Results.Ok(updated);
    }

    private static async Task<IResult> SelfAssignTicketAsync(
        Guid ticketId,
        [FromBody] SelfAssignTicketRequest request,
        CustomerManagementDbContext dbContext,
        HttpContext httpContext,
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
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid operation",
                Detail = "Authenticated user id is not available as a Guid claim.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var wrappedRequest = new AssignTicketRequest(actorUserId.Value, request.RowVersion);
        return await AssignTicketAsync(ticketId, wrappedRequest, dbContext, httpContext, cancellationToken);
    }

    private static async Task<IResult> EscalateTicketAsync(
        Guid ticketId,
        [FromBody] EscalateTicketRequest request,
        CustomerManagementDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request.RowVersion is null || request.RowVersion.Length == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["RowVersion is required."]
            });
        }

        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(row => row.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var now = DateTime.UtcNow;
        dbContext.Entry(ticket).Property(row => row.RowVersion).OriginalValue = request.RowVersion;

        if (!ticket.IsEscalated)
        {
            AddHistory(
                dbContext,
                ticket,
                "ticket.escalated",
                "isEscalated",
                "false",
                "true",
                httpContext.User,
                now);
        }

        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            AddHistory(
                dbContext,
                ticket,
                "ticket.escalation.reason",
                "reason",
                null,
                request.Reason.Trim(),
                httpContext.User,
                now);
        }

        ticket.IsEscalated = true;
        ticket.UpdatedAtUtc = now;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Concurrency conflict",
                Detail = "Ticket was updated by another request. Refresh and retry with the latest RowVersion.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var updated = await LoadTicketProjectionAsync(ticket.Id, dbContext, cancellationToken);
        return Results.Ok(updated);
    }

    private static async Task<IResult> ReopenTicketAsync(
        Guid ticketId,
        [FromBody] ReopenTicketRequest request,
        CustomerManagementDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request.RowVersion is null || request.RowVersion.Length == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["RowVersion is required."]
            });
        }

        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(row => row.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return Results.NotFound();
        }

        if (ticket.Status is not (TicketStatus.Resolved or TicketStatus.Closed))
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid operation",
                Detail = "Only resolved or closed tickets can be reopened.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var now = DateTime.UtcNow;
        dbContext.Entry(ticket).Property(row => row.RowVersion).OriginalValue = request.RowVersion;
        AddHistory(
            dbContext,
            ticket,
            "ticket.reopened",
            "status",
            ToApiStatus(ticket.Status),
            ToApiStatus(TicketStatus.InProgress),
            httpContext.User,
            now);

        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            AddHistory(
                dbContext,
                ticket,
                "ticket.reopen.reason",
                "reason",
                null,
                request.Reason.Trim(),
                httpContext.User,
                now);
        }

        ticket.Status = TicketStatus.InProgress;
        ticket.UpdatedAtUtc = now;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Concurrency conflict",
                Detail = "Ticket was updated by another request. Refresh and retry with the latest RowVersion.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var updated = await LoadTicketProjectionAsync(ticket.Id, dbContext, cancellationToken);
        return Results.Ok(updated);
    }

    private static async Task<IResult> GetTicketHistoryAsync(
        Guid ticketId,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var ticketScope = await dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.Id == ticketId)
            .Select(ticket => new { ticket.Id, ticket.CustomerId })
            .FirstOrDefaultAsync(cancellationToken);

        if (ticketScope is null)
        {
            return Results.NotFound();
        }

        if (ShouldApplyCustomerScope(httpContext.User))
        {
            var ownedCustomerIds = await ResolveOwnedCustomerIdsAsync(httpContext.User, dbContext, cancellationToken);
            if (!ownedCustomerIds.Contains(ticketScope.CustomerId))
            {
                return Results.NotFound();
            }
        }

        var entries = await dbContext.TicketHistoryEntries
            .AsNoTracking()
            .Where(entry => entry.TicketId == ticketId)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ThenByDescending(entry => entry.Id)
            .Select(entry => new TicketHistoryItemResponse(
                entry.Id,
                entry.ActionType,
                entry.FieldName,
                entry.OldValue,
                entry.NewValue,
                entry.ActorUserId,
                entry.ActorEmail,
                entry.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(new TicketHistoryResponse(ticketId, entries));
    }

    private static async Task<TicketResponse?> LoadTicketProjectionAsync(
        Guid ticketId,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return await dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.Category)
            .Include(ticket => ticket.Priority)
            .Include(ticket => ticket.AssignedToUser)
            .Where(ticket => ticket.Id == ticketId)
            .Select(ticket => new TicketResponse(
                ticket.Id,
                ticket.CustomerId,
                ticket.AssignedToUserId,
                ticket.AssignedToUser != null ? ticket.AssignedToUser.Email : null,
                ticket.CategoryId,
                ticket.Category.Name,
                ticket.Category.IsActive,
                ticket.PriorityId,
                ticket.Priority.Name,
                ticket.Priority.IsActive,
                ticket.Priority.SortOrder,
                ticket.Subject,
                ticket.Description,
                ToApiStatus(ticket.Status),
                ticket.IsEscalated,
                ticket.CreatedAtUtc,
                ticket.UpdatedAtUtc,
                ticket.RowVersion))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, string[]>> ValidateCreateTicketAsync(
        Guid customerId,
        Guid priorityId,
        CreateTicketRequest request,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCore(customerId, request.CategoryId, priorityId, request.Subject, request.Description);

        if (errors.Count > 0)
        {
            return errors;
        }

        var customerExists = await dbContext.Customers
            .AsNoTracking()
            .AnyAsync(customer => customer.Id == customerId, cancellationToken);
        if (!customerExists)
        {
            errors["customerId"] = ["Customer does not exist."];
        }

        var categoryExists = await dbContext.TicketCategories
            .AsNoTracking()
            .AnyAsync(category => category.Id == request.CategoryId && category.IsActive, cancellationToken);
        if (!categoryExists)
        {
            errors["categoryId"] = ["Category does not exist or is inactive."];
        }

        var priorityExists = await dbContext.TicketPriorities
            .AsNoTracking()
            .AnyAsync(priority => priority.Id == priorityId && priority.IsActive, cancellationToken);
        if (!priorityExists)
        {
            errors["priorityId"] = ["Priority does not exist or is inactive."];
        }

        return errors;
    }

    private static async Task<Guid?> ResolveDefaultPriorityIdAsync(
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var activePriorities = await dbContext.TicketPriorities
            .AsNoTracking()
            .Where(priority => priority.IsActive)
            .Select(priority => new { priority.Id, priority.Name, priority.SortOrder })
            .ToListAsync(cancellationToken);

        var normal = activePriorities
            .FirstOrDefault(priority => string.Equals(priority.Name, "Normal", StringComparison.OrdinalIgnoreCase));

        if (normal is not null)
        {
            return normal.Id;
        }

        return activePriorities
            .OrderBy(priority => priority.SortOrder)
            .Select(priority => (Guid?)priority.Id)
            .FirstOrDefault();
    }

    private static async Task<Dictionary<string, string[]>> ValidateUpdateCoreAsync(
        UpdateTicketCoreRequest request,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCore(Guid.NewGuid(), request.CategoryId, request.PriorityId, request.Subject, request.Description);
        errors.Remove("customerId");

        if (request.RowVersion is null || request.RowVersion.Length == 0)
        {
            errors["rowVersion"] = ["RowVersion is required."];
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var categoryExists = await dbContext.TicketCategories
            .AsNoTracking()
            .AnyAsync(category => category.Id == request.CategoryId && category.IsActive, cancellationToken);
        if (!categoryExists)
        {
            errors["categoryId"] = ["Category does not exist or is inactive."];
        }

        var priorityExists = await dbContext.TicketPriorities
            .AsNoTracking()
            .AnyAsync(priority => priority.Id == request.PriorityId && priority.IsActive, cancellationToken);
        if (!priorityExists)
        {
            errors["priorityId"] = ["Priority does not exist or is inactive."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateCore(
        Guid customerId,
        Guid categoryId,
        Guid priorityId,
        string subject,
        string description)
    {
        var errors = new Dictionary<string, string[]>();

        if (customerId == Guid.Empty)
        {
            errors["customerId"] = ["CustomerId is required."];
        }

        if (categoryId == Guid.Empty)
        {
            errors["categoryId"] = ["CategoryId is required."];
        }

        if (priorityId == Guid.Empty)
        {
            errors["priorityId"] = ["PriorityId is required."];
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            errors["subject"] = ["Subject is required."];
        }
        else if (subject.Trim().Length > 200)
        {
            errors["subject"] = ["Subject must be 200 characters or fewer."];
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            errors["description"] = ["Description is required."];
        }
        else if (description.Trim().Length > 4000)
        {
            errors["description"] = ["Description must be 4000 characters or fewer."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateAssignmentRequest(Guid assigneeUserId, byte[] rowVersion)
    {
        var errors = new Dictionary<string, string[]>();

        if (assigneeUserId == Guid.Empty)
        {
            errors["assigneeUserId"] = ["AssigneeUserId is required."];
        }

        if (rowVersion is null || rowVersion.Length == 0)
        {
            errors["rowVersion"] = ["RowVersion is required."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateTransitionRequest(string targetStatus, byte[] rowVersion)
    {
        var errors = new Dictionary<string, string[]>();

        if (!TryParseStatus(targetStatus, out _))
        {
            errors["targetStatus"] = ["TargetStatus is invalid."];
        }

        if (rowVersion is null || rowVersion.Length == 0)
        {
            errors["rowVersion"] = ["RowVersion is required."];
        }

        return errors;
    }

    private static bool TryParseStatus(string? value, out TicketStatus? status)
    {
        status = ParseStatus(value);
        return status.HasValue;
    }

    private static TicketStatus? ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "new" => TicketStatus.New,
            "in_progress" => TicketStatus.InProgress,
            "inprogress" => TicketStatus.InProgress,
            "waiting_on_customer" => TicketStatus.WaitingOnCustomer,
            "waitingoncustomer" => TicketStatus.WaitingOnCustomer,
            "resolved" => TicketStatus.Resolved,
            "closed" => TicketStatus.Closed,
            _ => null
        };
    }

    private static string ToApiStatus(TicketStatus status)
    {
        return status switch
        {
            TicketStatus.New => "new",
            TicketStatus.InProgress => "in_progress",
            TicketStatus.WaitingOnCustomer => "waiting_on_customer",
            TicketStatus.Resolved => "resolved",
            TicketStatus.Closed => "closed",
            _ => "new"
        };
    }

    private static bool CanTransition(TicketStatus current, TicketStatus target)
    {
        if (current == target)
        {
            return true;
        }

        return current switch
        {
            TicketStatus.New => target is TicketStatus.InProgress or TicketStatus.WaitingOnCustomer or TicketStatus.Resolved or TicketStatus.Closed,
            TicketStatus.InProgress => target is TicketStatus.WaitingOnCustomer or TicketStatus.Resolved or TicketStatus.Closed,
            TicketStatus.WaitingOnCustomer => target is TicketStatus.InProgress or TicketStatus.Resolved or TicketStatus.Closed,
            TicketStatus.Resolved => target is TicketStatus.Closed,
            TicketStatus.Closed => false,
            _ => false
        };
    }

    private static void AddHistory(
        CustomerManagementDbContext dbContext,
        Ticket ticket,
        string actionType,
        string fieldName,
        string? oldValue,
        string? newValue,
        ClaimsPrincipal actor,
        DateTime occurredAtUtc)
    {
        dbContext.TicketHistoryEntries.Add(new TicketHistoryEntry
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ActionType = actionType,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            ActorUserId = ResolveActorUserId(actor),
            ActorEmail = ResolveActorEmail(actor),
            OccurredAtUtc = occurredAtUtc
        });
    }

    private static Guid? ResolveActorUserId(ClaimsPrincipal actor)
    {
        var candidate = actor.FindFirstValue(ClaimTypes.NameIdentifier) ?? actor.FindFirstValue("sub");
        return Guid.TryParse(candidate, out var parsed) ? parsed : null;
    }

    private static string? ResolveActorEmail(ClaimsPrincipal actor)
    {
        return actor.FindFirstValue(ClaimTypes.Email) ?? actor.FindFirstValue("email") ?? actor.Identity?.Name;
    }

    private static bool IsAdmin(ClaimsPrincipal actor)
    {
        return actor.IsInRole(AuthRoles.Admin)
            || actor.Claims.Any(claim =>
                string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                && string.Equals(claim.Value, AuthRoles.Admin, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAgent(ClaimsPrincipal actor)
    {
        return actor.IsInRole(AuthRoles.Agent)
            || actor.Claims.Any(claim =>
                string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                && string.Equals(claim.Value, AuthRoles.Agent, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCustomer(ClaimsPrincipal actor)
    {
        return actor.IsInRole(AuthRoles.Customer)
            || actor.Claims.Any(claim =>
                string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                && string.Equals(claim.Value, AuthRoles.Customer, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ShouldApplyCustomerScope(ClaimsPrincipal actor)
    {
        if (IsAdmin(actor) || IsAgent(actor))
        {
            return false;
        }

        if (IsCustomer(actor))
        {
            return true;
        }

        return HasPermission(actor, Permissions.TicketsRead) || HasPermission(actor, Permissions.TicketsWrite);
    }

    private static bool HasPermission(ClaimsPrincipal actor, string permission)
    {
        return actor.Claims.Any(claim =>
            string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase)
            && string.Equals(claim.Value, permission, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<HashSet<Guid>> ResolveOwnedCustomerIdsAsync(
        ClaimsPrincipal actor,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actorUserId = ResolveActorUserId(actor);
        if (!actorUserId.HasValue)
        {
            return [];
        }

        return await dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.ApplicationUserId == actorUserId.Value)
            .Select(customer => customer.Id)
            .Distinct()
            .ToHashSetAsync(cancellationToken);
    }

    private static async Task<Guid?> ResolveCreateCustomerIdAsync(
        CreateTicketRequest request,
        ClaimsPrincipal actor,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (IsCustomer(actor))
        {
            var actorUserId = ResolveActorUserId(actor);
            if (!actorUserId.HasValue)
            {
                return null;
            }

            return await dbContext.Customers
                .AsNoTracking()
                .Where(customer => customer.ApplicationUserId == actorUserId.Value)
                .Select(customer => (Guid?)customer.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!request.CustomerUserId.HasValue || request.CustomerUserId.Value == Guid.Empty)
        {
            return null;
        }

        var selectedUserId = request.CustomerUserId.Value;

        var isCustomerRole = await (
                from userRole in dbContext.UserRoles.AsNoTracking()
                join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userRole.UserId == selectedUserId
                select role.Name)
            .AnyAsync(roleName => roleName == AuthRoles.Customer, cancellationToken);

        if (!isCustomerRole)
        {
            return null;
        }

        return await dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.ApplicationUserId == selectedUserId)
            .Select(customer => (Guid?)customer.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
