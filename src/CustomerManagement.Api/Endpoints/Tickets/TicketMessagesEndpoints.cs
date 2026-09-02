using System.Security.Claims;
using CustomerManagement.Api.Contracts.TicketMessages;
using CustomerManagement.Api.Domain.Tickets;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Tickets;

public static class TicketMessagesEndpoints
{
    public static IEndpointRouteBuilder MapTicketMessagesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ticket-messages").WithTags("Ticket Messages");

        group.MapGet(string.Empty, ListAsync)
            .RequireAuthorization($"Permission:{Permissions.TicketMessagesRead}")
            .WithName("ListTicketMessages")
            .WithSummary("List ticket conversation messages");

        group.MapPost(string.Empty, CreateAsync)
            .RequireAuthorization($"Permission:{Permissions.TicketMessagesWrite}")
            .WithName("CreateTicketMessage")
            .WithSummary("Post a ticket conversation message");

        return app;
    }

    private static async Task<IResult> ListAsync(
        [FromQuery] Guid ticketId,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateListRequest(ticketId, page, pageSize);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var actorUserId = ResolveActorUserId(httpContext.User);
        if (!actorUserId.HasValue)
        {
            return Results.Forbid();
        }

        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .Where(row => row.Id == ticketId)
            .Select(row => new TicketScopeProjection(
                row.Id,
                row.CustomerId,
                row.AssignedToUserId,
                row.Description,
                row.CreatedAtUtc,
                row.Customer.ApplicationUserId))
            .FirstOrDefaultAsync(cancellationToken);

        if (ticket is null)
        {
            return Results.NotFound();
        }

        var canView = await CanAccessTicketAsync(httpContext.User, actorUserId.Value, ticket, dbContext, cancellationToken);
        if (!canView.Allowed)
        {
            return canView.ReturnNotFound ? Results.NotFound() : Results.Forbid();
        }

        var initialSenderUserId = await ResolveInitialSenderUserIdAsync(ticket, dbContext, cancellationToken);
        if (!initialSenderUserId.HasValue)
        {
            return Results.NotFound();
        }

        var initialSender = await dbContext.Users
            .AsNoTracking()
            .Where(row => row.Id == initialSenderUserId.Value)
            .Select(row => new { row.Id, row.DisplayName, row.Email })
            .FirstOrDefaultAsync(cancellationToken);

        if (initialSender is null)
        {
            return Results.NotFound();
        }

        var initialSenderType = await ResolveSenderTypeAsync(initialSenderUserId.Value, ticket.CustomerApplicationUserId, dbContext, cancellationToken);

        var postedQuery = dbContext.TicketMessages
            .AsNoTracking()
            .Where(row => row.TicketId == ticketId);

        var postedCount = await postedQuery.CountAsync(cancellationToken);
        var totalCount = postedCount + 1;

        var start = (page - 1) * pageSize;
        var includeInitial = start == 0;
        var postedSkip = Math.Max(0, start - 1);
        var postedTake = includeInitial ? Math.Max(0, pageSize - 1) : pageSize;

        var items = new List<TicketMessageResponse>(pageSize);
        if (includeInitial)
        {
            items.Add(new TicketMessageResponse(
                Guid.Empty,
                ticket.Id,
                ToApiSenderType(initialSenderType),
                initialSender.Id,
                ResolveDisplayName(initialSender.DisplayName, initialSender.Email),
                ticket.Description,
                ticket.CreatedAtUtc,
                []));
        }

        if (postedTake > 0)
        {
            var postedItems = await postedQuery
                .OrderBy(row => row.CreatedAtUtc)
                .ThenBy(row => row.Id)
                .Skip(postedSkip)
                .Take(postedTake)
                .Select(row => new TicketMessageResponse(
                    row.Id,
                    row.TicketId,
                    ToApiSenderType(row.SenderType),
                    row.SenderUserId,
                    row.SenderDisplayName,
                    row.Body,
                    row.CreatedAtUtc,
                    row.RowVersion))
                .ToListAsync(cancellationToken);

            items.AddRange(postedItems);
        }

        return Results.Ok(new TicketMessageListResponse(page, pageSize, totalCount, items));
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateTicketMessageRequest request,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var actorUserId = ResolveActorUserId(httpContext.User);
        if (!actorUserId.HasValue)
        {
            return Results.Forbid();
        }

        if (IsCustomer(httpContext.User))
        {
            return Results.Forbid();
        }

        if (!IsAdmin(httpContext.User) && !IsAgent(httpContext.User))
        {
            return Results.Forbid();
        }

        var ticket = await dbContext.Tickets
            .FirstOrDefaultAsync(row => row.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Results.NotFound();
        }

        var scopeProjection = new TicketScopeProjection(
            ticket.Id,
            ticket.CustomerId,
            ticket.AssignedToUserId,
            ticket.Description,
            ticket.CreatedAtUtc,
            null);

        var canView = await CanAccessTicketAsync(httpContext.User, actorUserId.Value, scopeProjection, dbContext, cancellationToken);
        if (!canView.Allowed)
        {
            return canView.ReturnNotFound ? Results.NotFound() : Results.Forbid();
        }

        var sender = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == actorUserId.Value && row.IsActive, cancellationToken);

        if (sender is null)
        {
            return Results.NotFound();
        }

        var now = DateTime.UtcNow;
        var message = new TicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            SenderType = TicketMessageSenderType.Agent,
            SenderUserId = sender.Id,
            SenderDisplayName = ResolveDisplayName(sender.DisplayName, sender.Email),
            Body = request.Body.Trim(),
            CreatedAtUtc = now
        };

        dbContext.TicketMessages.Add(message);
        dbContext.TicketHistoryEntries.Add(new TicketHistoryEntry
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ActionType = "ticket.message.posted",
            FieldName = "messageId",
            OldValue = null,
            NewValue = message.Id.ToString(),
            ActorUserId = actorUserId,
            ActorEmail = ResolveActorEmail(httpContext.User),
            OccurredAtUtc = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new TicketMessageResponse(
            message.Id,
            message.TicketId,
            ToApiSenderType(message.SenderType),
            message.SenderUserId,
            message.SenderDisplayName,
            message.Body,
            message.CreatedAtUtc,
            message.RowVersion);

        return Results.Created($"/api/ticket-messages/{message.Id}", new CreateTicketMessageResponse(response));
    }

    private static Dictionary<string, string[]> ValidateListRequest(Guid ticketId, int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (ticketId == Guid.Empty)
        {
            errors["ticketId"] = ["TicketId is required."];
        }

        if (page < 1)
        {
            errors["page"] = ["Page must be greater than or equal to 1."];
        }

        if (pageSize < 1 || pageSize > 100)
        {
            errors["pageSize"] = ["PageSize must be between 1 and 100."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateCreateRequest(CreateTicketMessageRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.TicketId == Guid.Empty)
        {
            errors["ticketId"] = ["TicketId is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            errors["body"] = ["Body is required."];
        }
        else if (request.Body.Trim().Length > 4000)
        {
            errors["body"] = ["Body must be 4000 characters or fewer."];
        }

        return errors;
    }

    private static async Task<AccessResult> CanAccessTicketAsync(
        ClaimsPrincipal actor,
        Guid actorUserId,
        TicketScopeProjection ticket,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (IsAdmin(actor))
        {
            return AccessResult.Allow();
        }

        if (IsAgent(actor))
        {
            if (!ticket.AssignedToUserId.HasValue || ticket.AssignedToUserId.Value == actorUserId)
            {
                return AccessResult.Allow();
            }

            return AccessResult.Forbid();
        }

        if (IsCustomer(actor))
        {
            var ownedCustomerIds = await ResolveOwnedCustomerIdsAsync(actorUserId, dbContext, cancellationToken);
            return ownedCustomerIds.Contains(ticket.CustomerId)
                ? AccessResult.Allow()
                : AccessResult.NotFound();
        }

        return AccessResult.Forbid();
    }

    private static async Task<Guid?> ResolveInitialSenderUserIdAsync(
        TicketScopeProjection ticket,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var createdByUserId = await dbContext.TicketHistoryEntries
            .AsNoTracking()
            .Where(row => row.TicketId == ticket.Id && row.ActionType == "ticket.created")
            .OrderBy(row => row.OccurredAtUtc)
            .ThenBy(row => row.Id)
            .Select(row => row.ActorUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (createdByUserId.HasValue)
        {
            return createdByUserId.Value;
        }

        if (ticket.CustomerApplicationUserId.HasValue)
        {
            return ticket.CustomerApplicationUserId.Value;
        }

        if (ticket.AssignedToUserId.HasValue)
        {
            return ticket.AssignedToUserId.Value;
        }

        return null;
    }

    private static async Task<TicketMessageSenderType> ResolveSenderTypeAsync(
        Guid senderUserId,
        Guid? customerUserId,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (customerUserId.HasValue && customerUserId.Value == senderUserId)
        {
            return TicketMessageSenderType.Customer;
        }

        var isCustomerRole = await (
                from userRole in dbContext.UserRoles.AsNoTracking()
                join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userRole.UserId == senderUserId
                select role.Name)
            .AnyAsync(roleName => roleName == AuthRoles.Customer, cancellationToken);

        return isCustomerRole ? TicketMessageSenderType.Customer : TicketMessageSenderType.Agent;
    }

    private static string ToApiSenderType(TicketMessageSenderType senderType)
    {
        return senderType == TicketMessageSenderType.Customer ? "customer" : "agent";
    }

    private static string ResolveDisplayName(string displayName, string? email)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            return email;
        }

        return "Unknown";
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

    private static Task<HashSet<Guid>> ResolveOwnedCustomerIdsAsync(
        Guid actorUserId,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.ApplicationUserId == actorUserId)
            .Select(customer => customer.Id)
            .Distinct()
            .ToHashSetAsync(cancellationToken);
    }

    private sealed record TicketScopeProjection(
        Guid Id,
        Guid CustomerId,
        Guid? AssignedToUserId,
        string Description,
        DateTime CreatedAtUtc,
        Guid? CustomerApplicationUserId);

    private sealed record AccessResult(bool Allowed, bool ReturnNotFound)
    {
        public static AccessResult Allow() => new(true, false);
        public static AccessResult Forbid() => new(false, false);
        public static AccessResult NotFound() => new(false, true);
    }
}
