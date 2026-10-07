using System.Security.Claims;
using System.Text.Encodings.Web;
using CustomerManagement.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CustomerManagement.Api.Tests.Infrastructure;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(TestAuthDefaults.RoleHeader, out var roleValue) ||
            string.IsNullOrWhiteSpace(roleValue))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing X-User-Role header."));
        }

        var role = roleValue.ToString().Trim();
        var userId = ResolveUserId(role, Request.Headers[TestAuthDefaults.UserIdHeader]);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, $"integration-test-user-{role}"),
            new(ClaimTypes.Role, role)
        };

        foreach (var permission in ResolvePermissions(role))
        {
            claims.Add(new Claim("permission", permission));
        }

        var identity = new ClaimsIdentity(claims, TestAuthDefaults.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, TestAuthDefaults.SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static IReadOnlyList<string> ResolvePermissions(string role)
    {
        return role.Trim().ToLowerInvariant() switch
        {
            AuthRoles.Admin =>
            [
                Permissions.UsersManage,
                Permissions.RolesManage,
                Permissions.PermissionsManage,
                Permissions.AuditRead,
                Permissions.SettingsManage,
                Permissions.CustomersRead,
                Permissions.CustomersWrite,
                Permissions.TicketsRead,
                Permissions.TicketsWrite,
                Permissions.TicketsAssign,
                Permissions.TicketsEscalate,
                Permissions.TicketsClose,
                Permissions.TicketTaxonomyManage,
                Permissions.DashboardRead,
                Permissions.DashboardCustomerContextRead,
                Permissions.TicketTasksRead,
                Permissions.TicketTasksWrite,
                Permissions.TicketTasksComplete,
                Permissions.QuickRepliesRead,
                Permissions.QuickRepliesManage,
                Permissions.TicketInternalNotesRead,
                Permissions.TicketInternalNotesWrite,
                Permissions.TicketMentionsNotify,
                Permissions.TicketHandoffRequestCreate,
                Permissions.TicketHandoffRespond,
                Permissions.TicketHandoffForceAssign,
                Permissions.TicketMessagesRead,
                Permissions.TicketMessagesWrite,
                Permissions.FaqRead,
                Permissions.FaqManage,
                Permissions.HelpArticlesRead,
                Permissions.HelpArticlesManage,
                Permissions.GuidesRead,
                Permissions.GuidesManage,
                Permissions.FeedbackRead
            ],
            AuthRoles.Agent =>
            [
                Permissions.CustomersRead,
                Permissions.CustomersWrite,
                Permissions.TicketsRead,
                Permissions.TicketsWrite,
                Permissions.TicketsAssign,
                Permissions.TicketsEscalate,
                Permissions.TicketsClose,
                Permissions.DashboardRead,
                Permissions.DashboardCustomerContextRead,
                Permissions.TicketTasksRead,
                Permissions.TicketTasksWrite,
                Permissions.TicketTasksComplete,
                Permissions.QuickRepliesRead,
                Permissions.TicketInternalNotesRead,
                Permissions.TicketInternalNotesWrite,
                Permissions.TicketMentionsNotify,
                Permissions.TicketHandoffRequestCreate,
                Permissions.TicketHandoffRespond,
                Permissions.TicketMessagesRead,
                Permissions.TicketMessagesWrite,
                Permissions.FaqRead,
                Permissions.HelpArticlesRead,
                Permissions.GuidesRead,
                Permissions.FeedbackRead
            ],
            AuthRoles.Customer =>
            [
                Permissions.TicketsRead,
                Permissions.TicketsWrite,
                Permissions.TicketsClose,
                Permissions.TicketMessagesRead,
                Permissions.TicketMessagesWrite,
                Permissions.FaqRead,
                Permissions.HelpArticlesRead,
                Permissions.GuidesRead,
                Permissions.FeedbackSubmit
            ],
            _ => []
        };
    }

    private static Guid ResolveUserId(string role, string? headerValue)
    {
        if (Guid.TryParse(headerValue, out var parsed))
        {
            return parsed;
        }

        return role.Trim().ToLowerInvariant() switch
        {
            AuthRoles.Admin => Guid.Parse("11111111-1111-1111-1111-111111111111"),
            AuthRoles.Agent => Guid.Parse("22222222-2222-2222-2222-222222222222"),
            AuthRoles.Customer => Guid.Parse("33333333-3333-3333-3333-333333333333"),
            _ => Guid.Parse("44444444-4444-4444-4444-444444444444")
        };
    }
}
