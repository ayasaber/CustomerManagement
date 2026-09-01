using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.QuickReplies;
using CustomerManagement.Api.Domain.Dashboard;
using CustomerManagement.Api.Domain.Security;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using CustomerManagement.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerManagement.Api.Tests;

public sealed class QuickRepliesEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;
    private readonly CustomerManagementApiFactory _factory;

    public QuickRepliesEndpointsTests(CustomerManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AdminCanCreateUpdateAndDeactivateQuickReply()
    {
        var adminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        await SeedUsersAndRolesAsync();

        var createRequest = new CreateQuickReplyRequest(
            "Password Reset",
            "Please use the reset link from the sign in page.",
            ["auth", "reset"]);

        using var createHttp = NewRoleRequest(HttpMethod.Post, "/api/quick-replies", AuthRoles.Admin, adminUserId, createRequest);
        var createResponse = await _client.SendAsync(createHttp);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<QuickReplyResponse>();
        Assert.NotNull(created);
        Assert.True(created!.IsActive);

        await SetQuickReplyRowVersionAsync(created.Id, [1]);

        var updateRequest = new UpdateQuickReplyRequest(
            "Password Reset Updated",
            "Please use reset password, then confirm your email.",
            ["auth", "reset", "email"],
            false,
            [1]);

        using var updateHttp = NewRoleRequest(HttpMethod.Put, $"/api/quick-replies/{created.Id}", AuthRoles.Admin, adminUserId, updateRequest);
        var updateResponse = await _client.SendAsync(updateHttp);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<QuickReplyResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Password Reset Updated", updated!.Title);
        Assert.False(updated.IsActive);
        Assert.Contains("email", updated.Tags);
    }

    [Fact]
    public async Task AgentListAlwaysReturnsActiveOnly_EvenWhenActiveOnlyFalse()
    {
        var adminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var agentUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        await SeedUsersAndRolesAsync();

        await SeedQuickReplyAsync("Active Reply", "active", true, adminUserId, ["billing"]);
        await SeedQuickReplyAsync("Inactive Reply", "inactive", false, adminUserId, ["billing"]);

        using var agentListHttp = NewRoleRequest(
            HttpMethod.Get,
            "/api/quick-replies?activeOnly=false&page=1&pageSize=20",
            AuthRoles.Agent,
            agentUserId);
        var agentListResponse = await _client.SendAsync(agentListHttp);

        Assert.Equal(HttpStatusCode.OK, agentListResponse.StatusCode);
        var agentList = await agentListResponse.Content.ReadFromJsonAsync<QuickReplyListResponse>();
        Assert.NotNull(agentList);
        Assert.Single(agentList!.Items);
        Assert.All(agentList.Items, item => Assert.True(item.IsActive));

        using var adminListHttp = NewRoleRequest(
            HttpMethod.Get,
            "/api/quick-replies?activeOnly=false&page=1&pageSize=20",
            AuthRoles.Admin,
            adminUserId);
        var adminListResponse = await _client.SendAsync(adminListHttp);

        Assert.Equal(HttpStatusCode.OK, adminListResponse.StatusCode);
        var adminList = await adminListResponse.Content.ReadFromJsonAsync<QuickReplyListResponse>();
        Assert.NotNull(adminList);
        Assert.Equal(2, adminList!.TotalCount);
    }

    [Fact]
    public async Task AgentCannotCreateOrUpdateQuickReplies()
    {
        var adminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var agentUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        await SeedUsersAndRolesAsync();

        var existingId = await SeedQuickReplyAsync("Billing Reply", "body", true, adminUserId, ["billing"]);

        using var createHttp = NewRoleRequest(
            HttpMethod.Post,
            "/api/quick-replies",
            AuthRoles.Agent,
            agentUserId,
            new CreateQuickReplyRequest("New", "body", ["x"]));

        var createResponse = await _client.SendAsync(createHttp);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);

        using var updateHttp = NewRoleRequest(
            HttpMethod.Put,
            $"/api/quick-replies/{existingId}",
            AuthRoles.Agent,
            agentUserId,
            new UpdateQuickReplyRequest("Billing Reply", "body", ["billing"], true, [1]));

        var updateResponse = await _client.SendAsync(updateHttp);
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
    }

    [Fact]
    public async Task DuplicateTitle_ReturnsConflict()
    {
        var adminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        await SeedUsersAndRolesAsync();

        await SeedQuickReplyAsync("Order Update", "body", true, adminUserId, ["orders"]);

        var duplicateRequest = new CreateQuickReplyRequest(
            "order update",
            "new body",
            ["orders"]);

        using var createHttp = NewRoleRequest(HttpMethod.Post, "/api/quick-replies", AuthRoles.Admin, adminUserId, duplicateRequest);
        var response = await _client.SendAsync(createHttp);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithStaleRowVersion_ReturnsConflict()
    {
        var adminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        await SeedUsersAndRolesAsync();

        var replyId = await SeedQuickReplyAsync("Shipping Delay", "body", true, adminUserId, ["shipping"]);
        await SetQuickReplyRowVersionAsync(replyId, [7]);

        var request = new UpdateQuickReplyRequest(
            "Shipping Delay",
            "updated body",
            ["shipping"],
            true,
            [1]);

        using var updateHttp = NewRoleRequest(HttpMethod.Put, $"/api/quick-replies/{replyId}", AuthRoles.Admin, adminUserId, request);
        var response = await _client.SendAsync(updateHttp);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithTooManyTags_ReturnsBadRequestWithTagsLimitCode()
    {
        var adminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        await SeedUsersAndRolesAsync();

        var tags = Enumerable.Range(1, 21).Select(index => $"tag{index}").ToArray();
        var request = new CreateQuickReplyRequest("Tag Overflow", "body", tags);

        using var createHttp = NewRoleRequest(HttpMethod.Post, "/api/quick-replies", AuthRoles.Admin, adminUserId, request);
        var response = await _client.SendAsync(createHttp);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("tags_limit_exceeded", payload, StringComparison.OrdinalIgnoreCase);
    }

    private async Task SeedUsersAndRolesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        dbContext.QuickReplies.RemoveRange(dbContext.QuickReplies);
        dbContext.UserRoles.RemoveRange(dbContext.UserRoles);
        dbContext.Users.RemoveRange(dbContext.Users);
        await dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var adminRole = await dbContext.Roles.AsNoTracking().FirstAsync(role => role.Name == AuthRoles.Admin);
        var agentRole = await dbContext.Roles.AsNoTracking().FirstAsync(role => role.Name == AuthRoles.Agent);

        var adminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var agentUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        dbContext.Users.AddRange(
            new ApplicationUser
            {
                Id = adminUserId,
                UserName = "admin-quick@crm.local",
                Email = "admin-quick@crm.local",
                NormalizedUserName = "ADMIN-QUICK@CRM.LOCAL",
                NormalizedEmail = "ADMIN-QUICK@CRM.LOCAL",
                DisplayName = "Admin User",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new ApplicationUser
            {
                Id = agentUserId,
                UserName = "agent-quick@crm.local",
                Email = "agent-quick@crm.local",
                NormalizedUserName = "AGENT-QUICK@CRM.LOCAL",
                NormalizedEmail = "AGENT-QUICK@CRM.LOCAL",
                DisplayName = "Agent User",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

        dbContext.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = adminUserId, RoleId = adminRole.Id },
            new IdentityUserRole<Guid> { UserId = agentUserId, RoleId = agentRole.Id });

        await dbContext.SaveChangesAsync();
    }

    private async Task<Guid> SeedQuickReplyAsync(string title, string body, bool isActive, Guid createdByUserId, IReadOnlyList<string> tags)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();
        var now = DateTime.UtcNow;

        var quickReply = new QuickReply
        {
            Id = Guid.NewGuid(),
            Title = title,
            Body = body,
            TagsCsv = "," + string.Join(',', tags.Select(tag => tag.Trim().ToLowerInvariant())) + ",",
            IsActive = isActive,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.QuickReplies.Add(quickReply);
        await dbContext.SaveChangesAsync();
        return quickReply.Id;
    }

    private async Task SetQuickReplyRowVersionAsync(Guid quickReplyId, byte[] rowVersion)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        var reply = await dbContext.QuickReplies.FirstAsync(row => row.Id == quickReplyId);
        reply.RowVersion = rowVersion;
        await dbContext.SaveChangesAsync();
    }

    private static HttpRequestMessage NewRoleRequest(HttpMethod method, string uri, string role, Guid userId, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(TestAuthDefaults.RoleHeader, role);
        request.Headers.Add(TestAuthDefaults.UserIdHeader, userId.ToString());

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }
}
