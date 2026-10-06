using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.Auth;
using CustomerManagement.Api.Tests.Infrastructure;

namespace CustomerManagement.Api.Tests;

public sealed class AuthEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(CustomerManagementApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ReturnsCreated_AndIssuesTokens()
    {
        var request = NewRegisterRequest($"agent-{Guid.NewGuid():N}@crm.local", "agent");

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
        Assert.Contains("agent", payload.Roles, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("customers.read", payload.Permissions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_ReturnsCustomerPermissions_IncludingTicketMessagesWrite()
    {
        var request = NewRegisterRequest($"customer-perm-{Guid.NewGuid():N}@crm.local", "customer");

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(payload);
        Assert.Contains("customer", payload!.Roles, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("ticket-messages.write", payload.Permissions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_ReturnsConflict_WhenEmailAlreadyExists()
    {
        var email = $"dup-{Guid.NewGuid():N}@crm.local";
        var request = NewRegisterRequest(email, "customer");

        var first = await _client.PostAsJsonAsync("/api/auth/register", request);
        first.EnsureSuccessStatusCode();

        var second = await _client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenCredentialsAreInvalid()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("missing-user@crm.local", "wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsOk_WhenCredentialsAreValid()
    {
        var email = $"login-{Guid.NewGuid():N}@crm.local";
        var password = "Agent!23456";
        var register = NewRegisterRequest(email, "agent", password);

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", register);
        registerResponse.EnsureSuccessStatusCode();

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
    }

    [Fact]
    public async Task Refresh_ReturnsOk_AndRotatesRefreshToken()
    {
        var email = $"refresh-{Guid.NewGuid():N}@crm.local";
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest(email, "agent"));
        registerResponse.EnsureSuccessStatusCode();

        var initialTokens = await registerResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(initialTokens);

        var refreshResponse = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(initialTokens!.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(refreshed);
        Assert.NotEqual(initialTokens.RefreshToken, refreshed!.RefreshToken);

        var replayResponse = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(initialTokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var email = $"logout-{Guid.NewGuid():N}@crm.local";
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest(email, "customer"));
        registerResponse.EnsureSuccessStatusCode();

        var tokens = await registerResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokens);

        var logoutResponse = await _client.PostAsJsonAsync(
            "/api/auth/logout",
            new LogoutRequest(tokens!.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refreshResponse = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    private static RegisterRequest NewRegisterRequest(string email, string accountType, string password = "Agent!23456")
    {
        if (string.Equals(accountType, "customer", StringComparison.OrdinalIgnoreCase))
        {
            return new RegisterRequest(
                email,
                password,
                password,
                "Integration User",
                accountType,
                "Integration User",
                "Contoso",
                [new RegisterContactDetailRequest(1, email, "work", true)]);
        }

        return new RegisterRequest(
            email,
            password,
            password,
            "Integration User",
            accountType);
    }
}
