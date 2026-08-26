using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CustomerManagement.Api.Infrastructure.Auth;

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public const string PolicyPrefix = "Permission:";

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return base.GetPolicyAsync(policyName);
        }

        var permissionName = policyName[PolicyPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(permissionName))
        {
            return Task.FromResult<AuthorizationPolicy?>(null);
        }

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permissionName))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
