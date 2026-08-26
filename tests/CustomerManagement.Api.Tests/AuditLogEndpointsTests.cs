using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.Admin.Audit;
using CustomerManagement.Api.Contracts.Admin.Users;
using CustomerManagement.Api.Contracts.Auth;
using CustomerManagement.Api.Domain.Security;
using CustomerManagement.Api.Infrastructure.Persistence;
using CustomerManagement.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerManagement.Api.Tests;

public sealed class AuditLogEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;
    private readonly CustomerManagementApiFactory _factory;

    public AuditLogEndpointsTests(CustomerManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task LoginSuccessAndFailure_WriteAuditRows()
    {
        var email = $"audit-login-{Guid.NewGuid():N}@crm.local";
        const string password = "Agent!23456";

        var register = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, password, password, "Audit Login User", "agent"));
        register.EnsureSuccessStatusCode();

        var successResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, successResponse.StatusCode);

        var failureResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Wrong!23456"));
        Assert.Equal(HttpStatusCode.Unauthorized, failureResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        var loginEvents = dbContext.AuditLogEntries
            .Where(entry => entry.ActionType == "auth.login")
            .ToList();

        Assert.Contains(loginEvents, entry => entry.Result == "Success" && entry.EntityId != string.Empty);
        Assert.Contains(loginEvents, entry => entry.Result == "Failure");
    }

    [Fact]
    public async Task RoleAssignment_WritesAuditRow()
    {
        var email = $"audit-role-{Guid.NewGuid():N}@crm.local";

        using var createRequest = NewAdminJsonRequest(
            HttpMethod.Post,
            "/api/admin/users",
            new CreateAdminUserRequest(email, "Agent!23456", "Audit Role User", ["customer"]));
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();

        var createdUser = await createResponse.Content.ReadFromJsonAsync<AdminUserResponse>();
        Assert.NotNull(createdUser);

        var expectedRowVersion = new byte[] { 1, 2, 3, 4 };
        using (var setupScope = _factory.Services.CreateScope())
        {
            var setupDbContext = setupScope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();
            var userEntity = await setupDbContext.Users.FirstAsync(user => user.Id == createdUser!.Id);
            userEntity.RowVersion = expectedRowVersion;
            await setupDbContext.SaveChangesAsync();
        }

        using var updateRolesRequest = NewAdminJsonRequest(
            HttpMethod.Put,
            $"/api/admin/users/{createdUser!.Id}/roles",
            new UpdateUserRolesRequest(["agent"], expectedRowVersion));
        var updateResponse = await _client.SendAsync(updateRolesRequest);
        updateResponse.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        var roleAuditRow = dbContext.AuditLogEntries
            .FirstOrDefault(entry =>
                entry.ActionType == "admin.user.roles.update" &&
                entry.EntityId == createdUser.Id.ToString());

        Assert.NotNull(roleAuditRow);
        Assert.Equal("Success", roleAuditRow!.Result);
    }

    [Fact]
    public async Task AuditQuery_SupportsFiltersAndPagination()
    {
        var actionType = $"audit.filter.{Guid.NewGuid():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();
            dbContext.AuditLogEntries.AddRange(
                new AuditLogEntry
                {
                    Id = Guid.NewGuid(),
                    OccurredAtUtc = DateTime.UtcNow.AddMinutes(-3),
                    ActionType = actionType,
                    EntityName = "Test",
                    EntityId = "1",
                    Result = "Success"
                },
                new AuditLogEntry
                {
                    Id = Guid.NewGuid(),
                    OccurredAtUtc = DateTime.UtcNow.AddMinutes(-2),
                    ActionType = actionType,
                    EntityName = "Test",
                    EntityId = "2",
                    Result = "Failure"
                },
                new AuditLogEntry
                {
                    Id = Guid.NewGuid(),
                    OccurredAtUtc = DateTime.UtcNow.AddMinutes(-1),
                    ActionType = "other.action",
                    EntityName = "Test",
                    EntityId = "3",
                    Result = "Success"
                });

            await dbContext.SaveChangesAsync();
        }

        using var request = NewAdminRequest(HttpMethod.Get, $"/api/admin/audit-logs?actionType={actionType}&page=1&pageSize=1");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AuditLogResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.Page);
        Assert.Equal(1, payload.PageSize);
        Assert.Equal(2, payload.TotalCount);
        Assert.Single(payload.Items);
        Assert.Equal(actionType, payload.Items[0].ActionType);
    }

    [Fact]
    public async Task AuditQuery_ReturnsUnauthorizedAndForbidden_WhenAccessMissing()
    {
        var unauthorizedResponse = await _client.GetAsync("/api/admin/audit-logs");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);

        using var forbiddenRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/audit-logs");
        forbiddenRequest.Headers.Add(TestAuthDefaults.RoleHeader, "customer");

        var forbiddenResponse = await _client.SendAsync(forbiddenRequest);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
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
