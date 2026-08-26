using System.Text.Json;

namespace CustomerManagement.Gateway.Tests;

public sealed class GatewayRouteContractTests
{
    [Fact]
    public async Task OcelotRouteTable_ContainsAllCustomerManagementRoutes_WithoutConflicts()
    {
        var ocelotPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/CustomerManagement.Gateway/ocelot.json"));
        await using var stream = File.OpenRead(ocelotPath);
        using var json = await JsonDocument.ParseAsync(stream);

        var routes = json.RootElement.GetProperty("Routes")
            .EnumerateArray()
            .Select(route => new
            {
                UpstreamPathTemplate = route.GetProperty("UpstreamPathTemplate").GetString()!,
                Methods = route.GetProperty("UpstreamHttpMethod").EnumerateArray().Select(x => x.GetString()!).Order().ToArray()
            })
            .ToList();

        Assert.Equal(23, routes.Count);

        var customerRoutes = routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/customers", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Equal(7, customerRoutes.Count);

        var ticketRoutes = routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/tickets", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Equal(12, ticketRoutes.Count);

        var duplicates = routes
            .SelectMany(route => route.Methods.Select(method => $"{method}:{route.UpstreamPathTemplate}"))
            .GroupBy(key => key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToList();

        Assert.Empty(duplicates);

        Assert.Contains(customerRoutes, route => route.UpstreamPathTemplate.Equals("/api/customers", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["POST"]));
        Assert.Contains(customerRoutes, route => route.UpstreamPathTemplate.Equals("/api/customers/{customerId}", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "PUT"]));
        Assert.Contains(customerRoutes, route => route.UpstreamPathTemplate.Equals("/api/customers/{customerId}/contact-details", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET"]));
        Assert.Contains(customerRoutes, route => route.UpstreamPathTemplate.Equals("/api/customers/{customerId}/notes", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "POST"]));
        Assert.Contains(customerRoutes, route => route.UpstreamPathTemplate.Equals("/api/customers/{customerId}/attachments", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "POST"]));
        Assert.Contains(customerRoutes, route => route.UpstreamPathTemplate.Equals("/api/customers/{customerId}/attachments/{attachmentId}/content", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET"]));
        Assert.Contains(customerRoutes, route => route.UpstreamPathTemplate.Equals("/api/customers/{customerId}/interaction-history", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET"]));

        Assert.Contains(ticketRoutes, route => route.UpstreamPathTemplate.Equals("/api/tickets/categories", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "POST"]));
        Assert.Contains(ticketRoutes, route => route.UpstreamPathTemplate.Equals("/api/tickets/categories/{categoryId}", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["PUT"]));
        Assert.Contains(ticketRoutes, route => route.UpstreamPathTemplate.Equals("/api/tickets/priorities", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "POST"]));
        Assert.Contains(ticketRoutes, route => route.UpstreamPathTemplate.Equals("/api/tickets/priorities/{priorityId}", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["PUT"]));
        Assert.Contains(ticketRoutes, route => route.UpstreamPathTemplate.Equals("/api/tickets", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "POST"]));
        Assert.Contains(ticketRoutes, route => route.UpstreamPathTemplate.Equals("/api/tickets/{ticketId}", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "PUT"]));
        Assert.Contains(ticketRoutes, route => route.UpstreamPathTemplate.Equals("/api/tickets/{ticketId}/status", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["PUT"]));
        Assert.Contains(ticketRoutes, route => route.UpstreamPathTemplate.Equals("/api/tickets/{ticketId}/assign", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["PUT"]));
        Assert.Contains(ticketRoutes, route => route.UpstreamPathTemplate.Equals("/api/tickets/{ticketId}/self-assign", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["PUT"]));
        Assert.Contains(ticketRoutes, route => route.UpstreamPathTemplate.Equals("/api/tickets/{ticketId}/escalate", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["POST"]));
        Assert.Contains(ticketRoutes, route => route.UpstreamPathTemplate.Equals("/api/tickets/{ticketId}/reopen", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["POST"]));
        Assert.Contains(ticketRoutes, route => route.UpstreamPathTemplate.Equals("/api/tickets/{ticketId}/history", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET"]));

        Assert.Contains(routes, route => route.UpstreamPathTemplate.Equals("/api/auth/register", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["POST"]));
        Assert.Contains(routes, route => route.UpstreamPathTemplate.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["POST"]));
        Assert.Contains(routes, route => route.UpstreamPathTemplate.Equals("/api/auth/refresh", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["POST"]));
        Assert.Contains(routes, route => route.UpstreamPathTemplate.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["POST"]));
    }
}
