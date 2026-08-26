using CustomerManagement.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CustomerManagement.Api.Tests.Infrastructure.Auth;

public sealed class PermissionsPolicyProviderTests
{
    [Fact]
    public async Task GetPolicyAsync_ReturnsDynamicPolicy_ForPermissionPrefix()
    {
        var options = Options.Create(new AuthorizationOptions());
        var provider = new PermissionPolicyProvider(options);

        var policy = await provider.GetPolicyAsync("Permission:customers.read");

        Assert.NotNull(policy);
        var requirement = Assert.Single(policy!.Requirements.OfType<PermissionRequirement>());
        Assert.Equal("customers.read", requirement.Permission);
    }

    [Fact]
    public async Task GetPolicyAsync_Delegates_ForNonPrefixedPolicy()
    {
        var authorizationOptions = new AuthorizationOptions();
        authorizationOptions.AddPolicy("Known", policy => policy.RequireAuthenticatedUser());

        var provider = new PermissionPolicyProvider(Options.Create(authorizationOptions));
        var policy = await provider.GetPolicyAsync("Known");

        Assert.NotNull(policy);
        Assert.DoesNotContain(policy!.Requirements, requirement => requirement is PermissionRequirement);
    }
}
