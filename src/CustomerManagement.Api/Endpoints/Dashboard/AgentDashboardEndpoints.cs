using System.Security.Claims;
using CustomerManagement.Api.Contracts.Dashboard;
using CustomerManagement.Api.Domain.Customers;
using CustomerManagement.Api.Domain.Dashboard;
using CustomerManagement.Api.Domain.Tickets;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Dashboard;

public static class AgentDashboardEndpoints
{
    public static IEndpointRouteBuilder MapAgentDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard").WithTags("Dashboard");

        group.MapGet("/me/assigned-tickets", GetAssignedTicketsAsync)
            .RequireAuthorization("Permission:" + Permissions.DashboardRead)
            .WithName("GetDashboardAssignedTickets")
            .WithSummary("Get tickets assigned to the current agent");

        group.MapGet("/me/open-tasks", GetOpenTasksAsync)
            .RequireAuthorization("Permission:" + Permissions.DashboardRead)
            .WithName("GetDashboardOpenTasks")
            .WithSummary("Get open dashboard tasks summary for the current agent");

        group.MapGet("/tickets/{ticketId:guid}/customer-context", GetCustomerContextAsync)
            .RequireAuthorization("Permission:" + Permissions.DashboardCustomerContextRead)
            .WithName("GetDashboardTicketCustomerContext")
            .WithSummary("Get customer context for a dashboard ticket");

        return app;
    }

    private static async Task<IResult> GetAssignedTicketsAsync(
        string? status,
        bool? assignedToMeOnly,
        int? page,
        int? pageSize,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var resolvedPage = page.GetValueOrDefault(1);
        var resolvedPageSize = pageSize.GetValueOrDefault(20);
        var errors = new Dictionary<string, string[]>();

        if (resolvedPage < 1)
        {
            errors["page"] = ["Page must be greater than or equal to 1."];
        }

        if (resolvedPageSize < 1 || resolvedPageSize > 100)
        {
            errors["pageSize"] = ["PageSize must be between 1 and 100."];
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
        if (actorUserId is null)
        {
            return Results.Forbid();
        }

        var isAdmin = IsAdmin(httpContext.User);
        var query = dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.Category)
            .Include(ticket => ticket.Priority)
            .Include(ticket => ticket.Customer)
            .AsQueryable();

        if (!isAdmin)
        {
            query = query.Where(ticket => ticket.AssignedToUserId == actorUserId.Value);
        }
        else if (assignedToMeOnly.GetValueOrDefault(false))
        {
            query = query.Where(ticket => ticket.AssignedToUserId == actorUserId.Value);
        }

        if (parsedStatus.HasValue)
        {
            query = query.Where(ticket => ticket.Status == parsedStatus.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(ticket => ticket.UpdatedAtUtc)
            .ThenBy(ticket => ticket.Id)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .Select(ticket => new DashboardAssignedTicketItemResponse(
                ticket.Id,
                FormatTicketNumber(ticket.Id),
                ticket.Subject,
                NormalizeStatus(ticket.Status),
                ticket.Priority.Name,
                ticket.Category.Name,
                ticket.IsEscalated,
                ticket.CreatedAtUtc,
                ticket.UpdatedAtUtc,
                ticket.CustomerId,
                ticket.Customer.Name))
            .ToListAsync(cancellationToken);

        return Results.Ok(new DashboardAssignedTicketsResponse(totalCount, items));
    }

    private static async Task<IResult> GetOpenTasksAsync(
        bool? assignedToMeOnly,
        int? page,
        int? pageSize,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var resolvedPage = page.GetValueOrDefault(1);
        var resolvedPageSize = pageSize.GetValueOrDefault(20);
        var errors = new Dictionary<string, string[]>();

        if (resolvedPage < 1)
        {
            errors["page"] = ["Page must be greater than or equal to 1."];
        }

        if (resolvedPageSize < 1 || resolvedPageSize > 100)
        {
            errors["pageSize"] = ["PageSize must be between 1 and 100."];
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
        var query = dbContext.TicketTasks
            .AsNoTracking()
            .Where(task => task.Status == TicketTaskStatus.Open)
            .AsQueryable();

        if (!isAdmin || assignedToMeOnly.GetValueOrDefault(false))
        {
            query = query.Where(task => task.AssignedToUserId == actorUserId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(task => task.DueAtUtc)
            .ThenBy(task => task.Id)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .Select(task => new DashboardOpenTaskSummaryItemResponse(
                task.Id,
                task.TicketId,
                FormatTicketNumber(task.TicketId),
                task.Description,
                task.DueAtUtc,
                task.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(new DashboardOpenTaskSummaryResponse(totalCount, items));
    }

    private static async Task<IResult> GetCustomerContextAsync(
        Guid ticketId,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .Where(row => row.Id == ticketId)
            .Select(row => new
            {
                row.Id,
                row.CustomerId,
                row.AssignedToUserId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (ticket is null)
        {
            return Results.NotFound();
        }

        var actorUserId = ResolveActorUserId(httpContext.User);
        if (IsAdmin(httpContext.User))
        {
            if (!actorUserId.HasValue)
            {
                return Results.Forbid();
            }
        }
        else
        {
            if (!actorUserId.HasValue || ticket.AssignedToUserId != actorUserId.Value)
            {
                return Results.NotFound();
            }
        }

        var customer = await dbContext.Customers
            .AsNoTracking()
            .Where(row => row.Id == ticket.CustomerId)
            .Select(row => new
            {
                row.Id,
                row.Name,
                row.Company
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            return Results.NotFound();
        }

        var contacts = await dbContext.ContactDetails
            .AsNoTracking()
            .Where(row => row.CustomerId == customer.Id)
            .OrderByDescending(row => row.IsPrimary)
            .ThenBy(row => row.CreatedAtUtc)
            .Select(row => new
            {
                row.Channel,
                row.Value
            })
            .ToListAsync(cancellationToken);

        var interactions = await dbContext.CustomerInteractionEvents
            .AsNoTracking()
            .Where(row => row.CustomerId == customer.Id)
            .OrderByDescending(row => row.OccurredAtUtc)
            .ThenByDescending(row => row.Id)
            .Take(10)
            .Select(row => new DashboardCustomerInteractionItemResponse(
                row.Id,
                NormalizeInteractionType(row.Channel),
                row.Summary ?? string.Empty,
                row.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        var primaryEmail = contacts
            .Where(row => row.Channel == ContactChannel.Email)
            .Select(row => row.Value)
            .FirstOrDefault();

        var primaryPhone = contacts
            .Where(row => row.Channel is ContactChannel.Phone or ContactChannel.WhatsApp or ContactChannel.Sms)
            .Select(row => row.Value)
            .FirstOrDefault();

        var response = new DashboardCustomerContextResponse(
            ticket.Id,
            customer.Id,
            customer.Name,
            customer.Company,
            primaryEmail,
            primaryPhone,
            interactions);

        return Results.Ok(response);
    }

    private static string FormatTicketNumber(Guid ticketId)
    {
        return $"TKT-{ticketId:N}"[..12].ToUpperInvariant();
    }

    private static string NormalizeStatus(TicketStatus status)
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

    private static string NormalizeInteractionType(InteractionChannel channel)
    {
        return channel switch
        {
            InteractionChannel.Email => "email",
            InteractionChannel.WhatsApp => "whatsapp",
            InteractionChannel.LiveChat => "live_chat",
            InteractionChannel.Sms => "sms",
            InteractionChannel.WebForm => "web_form",
            _ => "unknown"
        };
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
