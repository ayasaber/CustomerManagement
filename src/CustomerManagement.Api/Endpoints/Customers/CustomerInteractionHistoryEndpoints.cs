using CustomerManagement.Api.Contracts.Customers;
using CustomerManagement.Api.Domain.Customers;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Customers;

public static class CustomerInteractionHistoryEndpoints
{
    public static IEndpointRouteBuilder MapCustomerInteractionHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers");

        group.MapGet("/{customerId:guid}/interaction-history", async (
            Guid customerId,
            string? channel,
            string? direction,
            int? page,
            int? pageSize,
            CustomerManagementDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var normalizedPage = !page.HasValue || page.Value <= 0 ? 1 : page.Value;
            var normalizedPageSize = !pageSize.HasValue || pageSize.Value <= 0 ? 50 : Math.Min(pageSize.Value, 200);

            var errors = ValidateFilters(channel, direction);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var customerExists = await dbContext.Customers
                .AsNoTracking()
                .AnyAsync(c => c.Id == customerId, cancellationToken);

            if (!customerExists)
            {
                return Results.NotFound();
            }

            var query = dbContext.CustomerInteractionEvents
                .AsNoTracking()
                .Where(i => i.CustomerId == customerId);

            if (!string.IsNullOrWhiteSpace(channel))
            {
                query = query.Where(i => i.Channel == ParseChannel(channel));
            }

            if (!string.IsNullOrWhiteSpace(direction))
            {
                query = query.Where(i => i.Direction == ParseDirection(direction));
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(i => i.OccurredAtUtc)
                .ThenByDescending(i => i.Id)
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .Select(i => new CustomerInteractionHistoryItemResponse(
                    i.Id,
                    NormalizeChannel(i.Channel),
                    NormalizeDirection(i.Direction),
                    i.OccurredAtUtc,
                    i.Summary,
                    i.SourceRef,
                    i.SourceSystem))
                .ToListAsync(cancellationToken);

            var response = new CustomerInteractionHistoryResponse(
                normalizedPage,
                normalizedPageSize,
                totalCount,
                items);

            return Results.Ok(response);
        })
        .RequireAuthorization("Permission:" + Permissions.CustomersRead)
        .WithName("GetCustomerInteractionHistory")
        .WithSummary("Get read-only interaction history for a customer with filters and pagination");

        return app;
    }

    private static Dictionary<string, string[]> ValidateFilters(string? channel, string? direction)
    {
        var errors = new Dictionary<string, string[]>();

        if (!string.IsNullOrWhiteSpace(channel) && !TryParseChannel(channel, out _))
        {
            errors["channel"] = ["Channel filter is invalid."];
        }

        if (!string.IsNullOrWhiteSpace(direction) && !TryParseDirection(direction, out _))
        {
            errors["direction"] = ["Direction filter is invalid."];
        }

        return errors;
    }

    private static bool TryParseChannel(string value, out InteractionChannel channel)
    {
        channel = value.Trim().ToLowerInvariant() switch
        {
            "email" => InteractionChannel.Email,
            "whatsapp" => InteractionChannel.WhatsApp,
            "live_chat" => InteractionChannel.LiveChat,
            "livechat" => InteractionChannel.LiveChat,
            "sms" => InteractionChannel.Sms,
            "web_form" => InteractionChannel.WebForm,
            "webform" => InteractionChannel.WebForm,
            "unknown" => InteractionChannel.Unknown,
            _ => default
        };

        return value.Trim().ToLowerInvariant() is "email" or "whatsapp" or "live_chat" or "livechat" or "sms" or "web_form" or "webform" or "unknown";
    }

    private static InteractionChannel ParseChannel(string value)
    {
        TryParseChannel(value, out var channel);
        return channel;
    }

    private static bool TryParseDirection(string value, out InteractionDirection direction)
    {
        direction = value.Trim().ToLowerInvariant() switch
        {
            "inbound" => InteractionDirection.Inbound,
            "outbound" => InteractionDirection.Outbound,
            "system" => InteractionDirection.System,
            "unknown" => InteractionDirection.Unknown,
            _ => default
        };

        return value.Trim().ToLowerInvariant() is "inbound" or "outbound" or "system" or "unknown";
    }

    private static InteractionDirection ParseDirection(string value)
    {
        TryParseDirection(value, out var direction);
        return direction;
    }

    private static string NormalizeChannel(InteractionChannel channel)
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

    private static string NormalizeDirection(InteractionDirection direction)
    {
        return direction switch
        {
            InteractionDirection.Inbound => "inbound",
            InteractionDirection.Outbound => "outbound",
            InteractionDirection.System => "system",
            _ => "unknown"
        };
    }
}
