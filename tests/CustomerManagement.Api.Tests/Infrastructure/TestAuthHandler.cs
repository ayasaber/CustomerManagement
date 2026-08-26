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
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "integration-test-user"),
            new(ClaimTypes.Name, "integration-test-user"),
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
                Permissions.TicketTaxonomyManage
            ],
            AuthRoles.Agent =>
            [
                Permissions.CustomersRead,
                Permissions.CustomersWrite,
                Permissions.TicketsRead,
                Permissions.TicketsWrite,
                Permissions.TicketsAssign,
                Permissions.TicketsEscalate,
                Permissions.TicketsClose
            ],
            _ => []
        };
    }
}
