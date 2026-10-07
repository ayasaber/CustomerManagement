using System.Text.Json;

namespace CustomerManagement.Gateway.Tests;

public sealed class GatewayRouteContractTests
{
    [Fact]
    public async Task OcelotRouteTable_ContainsAllCustomerManagementRoutes_WithoutConflicts()
    {
        var ocelotPath = ResolveOcelotPath();
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

        Assert.Equal(57, routes.Count);

        var customerRoutes = routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/customers", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Equal(7, customerRoutes.Count);

        var ticketRoutes = routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/tickets", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Equal(14, ticketRoutes.Count);

        var dashboardRoutes = routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/dashboard", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Equal(4, dashboardRoutes.Count);

        var ticketTaskRoutes = routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/ticket-tasks", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Equal(3, ticketTaskRoutes.Count);

        var quickReplyRoutes = routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/quick-replies", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Equal(2, quickReplyRoutes.Count);

        var ticketNoteRoutes = routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/ticket-notes", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Equal(4, ticketNoteRoutes.Count);

        var ticketMessageRoutes = routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/ticket-messages", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Single(ticketMessageRoutes);

        var faqRoutes = routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/faq", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Equal(2, faqRoutes.Count);

        var feedbackRoutes = routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/feedback", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Single(feedbackRoutes);

        var helpArticleRoutes = routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/help-articles", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Equal(2, helpArticleRoutes.Count);

        var guideRoutes = routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/guides", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Equal(2, guideRoutes.Count);

        var knowledgeBaseSearchRoutes = routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/knowledge-base", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Single(knowledgeBaseSearchRoutes);

        var adminRoutes = routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Equal(10, adminRoutes.Count);

        var duplicates = routes
            .SelectMany(route => route.Methods.Select(method => $"{method}:{route.UpstreamPathTemplate}"))
            .GroupBy(key => key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToList();

        Assert.Empty(duplicates);

        Assert.Contains(customerRoutes, route => route.UpstreamPathTemplate.Equals("/api/customers", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "POST"]));
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
        Assert.Contains(ticketRoutes, route => route.UpstreamPathTemplate.Equals("/api/tickets/{ticketId}/attachments", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "POST"]));
        Assert.Contains(ticketRoutes, route => route.UpstreamPathTemplate.Equals("/api/tickets/{ticketId}/attachments/{attachmentId}/content", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET"]));

        Assert.Contains(dashboardRoutes, route => route.UpstreamPathTemplate.Equals("/api/dashboard/me/assigned-tickets", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET"]));
        Assert.Contains(dashboardRoutes, route => route.UpstreamPathTemplate.Equals("/api/dashboard/me/open-tasks", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET"]));
        Assert.Contains(dashboardRoutes, route => route.UpstreamPathTemplate.Equals("/api/dashboard/tickets/{ticketId}/customer-context", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET"]));
        Assert.Contains(dashboardRoutes, route => route.UpstreamPathTemplate.Equals("/api/dashboard/agents", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET"]));

        Assert.Contains(ticketTaskRoutes, route => route.UpstreamPathTemplate.Equals("/api/ticket-tasks", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "POST"]));
        Assert.Contains(ticketTaskRoutes, route => route.UpstreamPathTemplate.Equals("/api/ticket-tasks/{taskId}", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["PUT"]));
        Assert.Contains(ticketTaskRoutes, route => route.UpstreamPathTemplate.Equals("/api/ticket-tasks/{taskId}/complete", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["PUT"]));

        Assert.Contains(quickReplyRoutes, route => route.UpstreamPathTemplate.Equals("/api/quick-replies", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "POST"]));
        Assert.Contains(quickReplyRoutes, route => route.UpstreamPathTemplate.Equals("/api/quick-replies/{quickReplyId}", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["PUT"]));

        Assert.Contains(ticketNoteRoutes, route => route.UpstreamPathTemplate.Equals("/api/ticket-notes", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "POST"]));
        Assert.Contains(ticketNoteRoutes, route => route.UpstreamPathTemplate.Equals("/api/ticket-notes/{noteId}/handoff-requests", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["POST"]));
        Assert.Contains(ticketNoteRoutes, route => route.UpstreamPathTemplate.Equals("/api/ticket-notes/handoff-requests/me", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET"]));
        Assert.Contains(ticketNoteRoutes, route => route.UpstreamPathTemplate.Equals("/api/ticket-notes/handoff-requests/{handoffRequestId}/respond", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["POST"]));

        Assert.Contains(ticketMessageRoutes, route => route.UpstreamPathTemplate.Equals("/api/ticket-messages", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "POST"]));

        Assert.Contains(faqRoutes, route => route.UpstreamPathTemplate.Equals("/api/faq", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "POST"]));
        Assert.Contains(faqRoutes, route => route.UpstreamPathTemplate.Equals("/api/faq/{faqId}", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["PUT"]));

        Assert.Contains(feedbackRoutes, route => route.UpstreamPathTemplate.Equals("/api/feedback", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "POST"]));

        Assert.Contains(helpArticleRoutes, route => route.UpstreamPathTemplate.Equals("/api/help-articles", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "POST"]));
        Assert.Contains(helpArticleRoutes, route => route.UpstreamPathTemplate.Equals("/api/help-articles/{articleId}", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["PUT"]));

        Assert.Contains(guideRoutes, route => route.UpstreamPathTemplate.Equals("/api/guides", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "POST"]));
        Assert.Contains(guideRoutes, route => route.UpstreamPathTemplate.Equals("/api/guides/{guideId}", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["PUT"]));

        Assert.Contains(knowledgeBaseSearchRoutes, route => route.UpstreamPathTemplate.Equals("/api/knowledge-base/search", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET"]));

        Assert.Contains(adminRoutes, route => route.UpstreamPathTemplate.Equals("/api/admin/users", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "POST"]));
        Assert.Contains(adminRoutes, route => route.UpstreamPathTemplate.Equals("/api/admin/users/{userId}", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["DELETE", "GET", "PUT"]));
        Assert.Contains(adminRoutes, route => route.UpstreamPathTemplate.Equals("/api/admin/users/{userId}/roles", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["PUT"]));
        Assert.Contains(adminRoutes, route => route.UpstreamPathTemplate.Equals("/api/admin/permissions", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "POST"]));
        Assert.Contains(adminRoutes, route => route.UpstreamPathTemplate.Equals("/api/admin/permissions/{permissionId}", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["PUT"]));
        Assert.Contains(adminRoutes, route => route.UpstreamPathTemplate.Equals("/api/admin/roles", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET"]));
        Assert.Contains(adminRoutes, route => route.UpstreamPathTemplate.Equals("/api/admin/roles/{roleId}/permissions", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "PUT"]));
        Assert.Contains(adminRoutes, route => route.UpstreamPathTemplate.Equals("/api/admin/audit-logs", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET"]));
        Assert.Contains(adminRoutes, route => route.UpstreamPathTemplate.Equals("/api/admin/system-settings", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET"]));
        Assert.Contains(adminRoutes, route => route.UpstreamPathTemplate.Equals("/api/admin/system-settings/{key}", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["GET", "PUT"]));

        Assert.Contains(routes, route => route.UpstreamPathTemplate.Equals("/api/auth/register", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["POST"]));
        Assert.Contains(routes, route => route.UpstreamPathTemplate.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["POST"]));
        Assert.Contains(routes, route => route.UpstreamPathTemplate.Equals("/api/auth/refresh", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["POST"]));
        Assert.Contains(routes, route => route.UpstreamPathTemplate.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase) && route.Methods.SequenceEqual(["POST"]));
    }

    private static string ResolveOcelotPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "CustomerManagement.Gateway", "ocelot.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate src/CustomerManagement.Gateway/ocelot.json from test output directory.");
    }
}
