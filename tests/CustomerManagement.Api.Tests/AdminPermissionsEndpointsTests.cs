using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.Admin.Permissions;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using CustomerManagement.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerManagement.Api.Tests;

public sealed class AdminPermissionsEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;
    private readonly CustomerManagementApiFactory _factory;

    public AdminPermissionsEndpointsTests(CustomerManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PermissionCrud_AndDuplicateNameConflict()
    {
        var createRequest = new CreatePermissionRequest("Orders.Read", "Read order records");
        using var createHttpRequest = NewAdminJsonRequest(HttpMethod.Post, "/api/admin/permissions", createRequest);
        var createResponse = await _client.SendAsync(createHttpRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<PermissionResponse>();
        Assert.NotNull(created);
        Assert.Equal("orders.read", created!.Name);

        using var duplicateHttpRequest = NewAdminJsonRequest(HttpMethod.Post, "/api/admin/permissions", new CreatePermissionRequest("orders.read", null));
        var duplicateResponse = await _client.SendAsync(duplicateHttpRequest);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        using var updateHttpRequest = NewAdminJsonRequest(
            HttpMethod.Put,
            $"/api/admin/permissions/{created.Id}",
            new UpdatePermissionRequest("orders.viewer", "Updated"));
        var updateResponse = await _client.SendAsync(updateHttpRequest);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<PermissionResponse>();
        Assert.NotNull(updated);
        Assert.Equal("orders.viewer", updated!.Name);
        Assert.Equal("Updated", updated.Description);
    }

    [Fact]
    public async Task RolePermissionAssignment_RequiresExistingIds_AndReturnsUpdatedSet()
    {
        var createdPermission = await CreatePermissionAsync("tickets.resolve");

        var agentRoleId = await GetRoleIdAsync(AuthRoles.Agent);
        using var assignHttpRequest = NewAdminJsonRequest(
            HttpMethod.Put,
            $"/api/admin/roles/{agentRoleId}/permissions",
            new UpdateRolePermissionsRequest([createdPermission.Id]));

        var assignResponse = await _client.SendAsync(assignHttpRequest);
        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);

        var rolePermissions = await assignResponse.Content.ReadFromJsonAsync<RolePermissionsResponse>();
        Assert.NotNull(rolePermissions);
        Assert.Equal(agentRoleId, rolePermissions!.RoleId);
        Assert.Contains(rolePermissions.Permissions, permission => permission.Id == createdPermission.Id);

        using var getHttpRequest = NewAdminRequest(HttpMethod.Get, $"/api/admin/roles/{agentRoleId}/permissions");
        var getResponse = await _client.SendAsync(getHttpRequest);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<RolePermissionsResponse>();
        Assert.NotNull(fetched);
        Assert.Contains(fetched!.Permissions, permission => permission.Id == createdPermission.Id);

        using var invalidAssignRequest = NewAdminJsonRequest(
            HttpMethod.Put,
            $"/api/admin/roles/{agentRoleId}/permissions",
            new UpdateRolePermissionsRequest([Guid.NewGuid()]));

        var invalidAssignResponse = await _client.SendAsync(invalidAssignRequest);
        Assert.Equal(HttpStatusCode.BadRequest, invalidAssignResponse.StatusCode);
    }

    private async Task<PermissionResponse> CreatePermissionAsync(string name)
    {
        using var createHttpRequest = NewAdminJsonRequest(
            HttpMethod.Post,
            "/api/admin/permissions",
            new CreatePermissionRequest(name, null));

        var response = await _client.SendAsync(createHttpRequest);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PermissionResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task<Guid> GetRoleIdAsync(string roleName)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        var roleId = await dbContext.Roles
            .Where(role => role.Name == roleName)
            .Select(role => role.Id)
            .FirstAsync();

        return roleId;
    }

    private static HttpRequestMessage NewAdminJsonRequest(HttpMethod method, string uri, object body)
    {
        var request = NewAdminRequest(method, uri);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private static HttpRequestMessage NewAdminRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(TestAuthDefaults.RoleHeader, "admin");
        return request;
    }
}
