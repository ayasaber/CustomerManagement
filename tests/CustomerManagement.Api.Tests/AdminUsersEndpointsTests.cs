using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CustomerManagement.Api.Contracts.Admin.Users;
using CustomerManagement.Api.Contracts.Auth;
using CustomerManagement.Api.Tests.Infrastructure;

namespace CustomerManagement.Api.Tests;

public sealed class AdminUsersEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;

    public AdminUsersEndpointsTests(CustomerManagementApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateUser_ReturnsCreated_AndDuplicateEmailReturnsConflict()
    {
        var email = $"admin-create-{Guid.NewGuid():N}@crm.local";
        var request = NewCreateRequest(email, "Agent!23456", "Create Agent", ["agent"]);

        using var createHttpRequest = NewAdminJsonRequest(HttpMethod.Post, "/api/admin/users", request);
        var createResponse = await _client.SendAsync(createHttpRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminUserResponse>();
        Assert.NotNull(created);
        Assert.Equal(email, created!.Email);
        Assert.Contains("agent", created.Roles, StringComparer.OrdinalIgnoreCase);

        using var duplicateHttpRequest = NewAdminJsonRequest(HttpMethod.Post, "/api/admin/users", request);
        var duplicateResponse = await _client.SendAsync(duplicateHttpRequest);

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task ListUsers_SupportsPaginationAndRoleFilter()
    {
        await CreateAdminUserAsync($"agent-a-{Guid.NewGuid():N}@crm.local", "Agent A", ["agent"]);
        await CreateAdminUserAsync($"agent-b-{Guid.NewGuid():N}@crm.local", "Agent B", ["agent"]);
        await CreateAdminUserAsync($"customer-{Guid.NewGuid():N}@crm.local", "Customer A", ["customer"]);

        using var request = NewAdminRequest(HttpMethod.Get, "/api/admin/users?page=1&pageSize=1&role=agent");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, payload.GetProperty("page").GetInt32());
        Assert.Equal(1, payload.GetProperty("pageSize").GetInt32());
        Assert.True(payload.GetProperty("totalCount").GetInt32() >= 2);

        var items = payload.GetProperty("items");
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.Single(items.EnumerateArray());

        var firstItem = items.EnumerateArray().First();
        var roles = firstItem.GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ToList();
        Assert.Contains("agent", roles, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateUser_ReturnsConflict_WhenRowVersionIsStale()
    {
        var created = await CreateAdminUserAsync(
            $"stale-{Guid.NewGuid():N}@crm.local",
            "Stale User",
            ["agent"]);

        var update = new UpdateAdminUserRequest("Stale User Updated", true, [1, 2, 3, 4]);

        using var updateRequest = NewAdminJsonRequest(HttpMethod.Put, $"/api/admin/users/{created.Id}", update);
        var response = await _client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRoles_ReturnsBadRequest_ForUnknownRoleNames()
    {
        var created = await CreateAdminUserAsync(
            $"roles-invalid-{Guid.NewGuid():N}@crm.local",
            "Role User",
            ["agent"]);

        var request = new UpdateUserRolesRequest(["agent", "unknown-role"], [1, 2, 3, 4]);
        using var updateRequest = NewAdminJsonRequest(HttpMethod.Put, $"/api/admin/users/{created.Id}/roles", request);
        var response = await _client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errorObject = payload.GetProperty("errors");
        var hasPerRoleKey = errorObject.EnumerateObject()
            .Any(property => property.Name.StartsWith("roles[", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasPerRoleKey, "Expected at least one per-role validation key in errors.");
    }

    [Fact]
    public async Task SoftDeactivateUser_DeniesSubsequentLogin()
    {
        var email = $"deactivate-{Guid.NewGuid():N}@crm.local";
        const string password = "Agent!23456";

        var created = await CreateAdminUserAsync(email, "Deactivate Me", ["agent"], password);

        using var deactivateRequest = NewAdminRequest(HttpMethod.Delete, $"/api/admin/users/{created.Id}");
        var deactivateResponse = await _client.SendAsync(deactivateRequest);
        Assert.Equal(HttpStatusCode.NoContent, deactivateResponse.StatusCode);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task SoftDeactivateUser_BlocksSelfDeactivation()
    {
        var email = $"self-deactivate-{Guid.NewGuid():N}@crm.local";
        var created = await CreateAdminUserAsync(email, "Self Admin", ["admin"], "Admin!23456");

        using var deactivateRequest = NewAdminRequest(HttpMethod.Delete, $"/api/admin/users/{created.Id}", created.Id);
        var response = await _client.SendAsync(deactivateRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<AdminUserResponse> CreateAdminUserAsync(
        string email,
        string displayName,
        IReadOnlyList<string> roles,
        string password = "Agent!23456")
    {
        var request = NewCreateRequest(email, password, displayName, roles);
        using var httpRequest = NewAdminJsonRequest(HttpMethod.Post, "/api/admin/users", request);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AdminUserResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private static CreateAdminUserRequest NewCreateRequest(
        string email,
        string password,
        string displayName,
        IReadOnlyList<string> roles)
    {
        return new CreateAdminUserRequest(email, password, displayName, roles);
    }

    private static HttpRequestMessage NewAdminJsonRequest(HttpMethod method, string uri, object body, Guid? actorUserId = null)
    {
        var request = NewAdminRequest(method, uri, actorUserId);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private static HttpRequestMessage NewAdminRequest(HttpMethod method, string uri, Guid? actorUserId = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(TestAuthDefaults.RoleHeader, "admin");
        if (actorUserId.HasValue)
        {
            request.Headers.Add(TestAuthDefaults.UserIdHeader, actorUserId.Value.ToString());
        }

        return request;
    }
}
