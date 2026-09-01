using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.Dashboard;
using CustomerManagement.Api.Contracts.TicketTasks;
using CustomerManagement.Api.Domain.Customers;
using CustomerManagement.Api.Domain.Dashboard;
using CustomerManagement.Api.Domain.Security;
using CustomerManagement.Api.Domain.Tickets;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using CustomerManagement.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerManagement.Api.Tests;

public sealed class TicketTasksEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;
    private readonly CustomerManagementApiFactory _factory;

    public TicketTasksEndpointsTests(CustomerManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateListUpdateComplete_Works_ForAgentAndOwnTask()
    {
        var agentUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var seeded = await SeedScenarioAsync(agentUserId, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        var createRequest = new CreateTicketTaskRequest(
            seeded.AgentTicketId,
            "Follow up customer tomorrow",
            DateTime.UtcNow.AddHours(8),
            null);

        using var createHttp = NewRoleRequest(HttpMethod.Post, "/api/ticket-tasks", "agent", agentUserId, createRequest);
        var createResponse = await _client.SendAsync(createHttp);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<TicketTaskResponse>();
        Assert.NotNull(created);
        Assert.Equal("open", created!.Status);
        Assert.Equal(agentUserId, created.AssignedToUserId);

        await SetTaskRowVersionAsync(created.Id, [1]);

        using var listHttp = NewRoleRequest(HttpMethod.Get, "/api/ticket-tasks?assignedToMeOnly=true", "agent", agentUserId);
        var listResponse = await _client.SendAsync(listHttp);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var listed = await listResponse.Content.ReadFromJsonAsync<TicketTaskListResponse>();
        Assert.NotNull(listed);
        Assert.True(listed!.TotalCount >= 1);
        var listedTask = listed.Items.Single(item => item.Id == created.Id);
        Assert.Equal("Follow up customer tomorrow", listedTask.Description);

        var updateRequest = new UpdateTicketTaskRequest(
            "Follow up customer by phone",
            created.DueAtUtc.AddHours(1),
            agentUserId,
            [1]);

        using var updateHttp = NewRoleRequest(HttpMethod.Put, $"/api/ticket-tasks/{created.Id}", "agent", agentUserId, updateRequest);
        var updateResponse = await _client.SendAsync(updateHttp);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<TicketTaskResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Follow up customer by phone", updated!.Description);

        await SetTaskRowVersionAsync(updated.Id, [2]);

        var completeRequest = new CompleteTicketTaskRequest([2]);
        using var completeHttp = NewRoleRequest(HttpMethod.Put, $"/api/ticket-tasks/{created.Id}/complete", "agent", agentUserId, completeRequest);
        var completeResponse = await _client.SendAsync(completeHttp);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var completed = await completeResponse.Content.ReadFromJsonAsync<TicketTaskResponse>();
        Assert.NotNull(completed);
        Assert.Equal("done", completed!.Status);
        Assert.Equal(agentUserId, completed.CompletedByUserId);
    }

    [Fact]
    public async Task Complete_ReturnsForbidden_WhenAgentCompletesOthersTask()
    {
        var agentUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var otherAgentUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var seeded = await SeedScenarioAsync(agentUserId, otherAgentUserId);

        var taskId = await SeedTaskAsync(seeded.OtherAgentTicketId, otherAgentUserId, otherAgentUserId, "Other agent task");

        using var completeHttp = NewRoleRequest(
            HttpMethod.Put,
            $"/api/ticket-tasks/{taskId}/complete",
            "agent",
            agentUserId,
            new CompleteTicketTaskRequest([1]));

        var response = await _client.SendAsync(completeHttp);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Complete_ReturnsConflict_WhenAlreadyDone()
    {
        var agentUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var seeded = await SeedScenarioAsync(agentUserId, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        var taskId = await SeedTaskAsync(seeded.AgentTicketId, agentUserId, agentUserId, "Already done", TicketTaskStatus.Done);

        using var completeHttp = NewRoleRequest(
            HttpMethod.Put,
            $"/api/ticket-tasks/{taskId}/complete",
            "agent",
            agentUserId,
            new CompleteTicketTaskRequest([1]));

        var response = await _client.SendAsync(completeHttp);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DashboardOpenTasks_ReturnsRealOpenTasks_AfterStory15()
    {
        var agentUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var seeded = await SeedScenarioAsync(agentUserId, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        await SeedTaskAsync(seeded.AgentTicketId, agentUserId, agentUserId, "Open dashboard task");

        using var dashboardHttp = NewRoleRequest(
            HttpMethod.Get,
            "/api/dashboard/me/open-tasks?page=1&pageSize=20",
            "agent",
            agentUserId);

        var dashboardResponse = await _client.SendAsync(dashboardHttp);
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);

        var payload = await dashboardResponse.Content.ReadFromJsonAsync<DashboardOpenTaskSummaryResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.TotalCount);
        Assert.Single(payload.Items);
        Assert.Equal("Open dashboard task", payload.Items[0].Description);
    }

    [Fact]
    public async Task Create_ReturnsNotFound_WhenTicketMissing()
    {
        var agentUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedScenarioAsync(agentUserId, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        var createRequest = new CreateTicketTaskRequest(
            Guid.NewGuid(),
            "Task",
            DateTime.UtcNow.AddHours(4),
            null);

        using var createHttp = NewRoleRequest(HttpMethod.Post, "/api/ticket-tasks", "agent", agentUserId, createRequest);
        var response = await _client.SendAsync(createHttp);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<(Guid AgentTicketId, Guid OtherAgentTicketId)> SeedScenarioAsync(Guid agentUserId, Guid otherAgentUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        dbContext.TicketTasks.RemoveRange(dbContext.TicketTasks);
        dbContext.TicketHistoryEntries.RemoveRange(dbContext.TicketHistoryEntries);
        dbContext.Tickets.RemoveRange(dbContext.Tickets);
        dbContext.CustomerInteractionEvents.RemoveRange(dbContext.CustomerInteractionEvents);
        dbContext.ContactDetails.RemoveRange(dbContext.ContactDetails);
        dbContext.Customers.RemoveRange(dbContext.Customers);
        dbContext.TicketPriorities.RemoveRange(dbContext.TicketPriorities);
        dbContext.TicketCategories.RemoveRange(dbContext.TicketCategories);
        dbContext.UserRoles.RemoveRange(dbContext.UserRoles);
        dbContext.Users.RemoveRange(dbContext.Users);
        await dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;

        var adminRole = await dbContext.Roles.AsNoTracking().FirstAsync(role => role.Name == AuthRoles.Admin);
        var agentRole = await dbContext.Roles.AsNoTracking().FirstAsync(role => role.Name == AuthRoles.Agent);

        var agentUser = new ApplicationUser
        {
            Id = agentUserId,
            UserName = $"agent-{agentUserId:N}@crm.local",
            Email = $"agent-{agentUserId:N}@crm.local",
            NormalizedUserName = $"AGENT-{agentUserId:N}@CRM.LOCAL",
            NormalizedEmail = $"AGENT-{agentUserId:N}@CRM.LOCAL",
            DisplayName = "Agent One",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var otherAgentUser = new ApplicationUser
        {
            Id = otherAgentUserId,
            UserName = $"agent-{otherAgentUserId:N}@crm.local",
            Email = $"agent-{otherAgentUserId:N}@crm.local",
            NormalizedUserName = $"AGENT-{otherAgentUserId:N}@CRM.LOCAL",
            NormalizedEmail = $"AGENT-{otherAgentUserId:N}@CRM.LOCAL",
            DisplayName = "Agent Two",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var adminUser = new ApplicationUser
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserName = "admin-seeded@crm.local",
            Email = "admin-seeded@crm.local",
            NormalizedUserName = "ADMIN-SEEDED@CRM.LOCAL",
            NormalizedEmail = "ADMIN-SEEDED@CRM.LOCAL",
            DisplayName = "Admin",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Users.AddRange(agentUser, otherAgentUser, adminUser);
        dbContext.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = agentUserId, RoleId = agentRole.Id },
            new IdentityUserRole<Guid> { UserId = otherAgentUserId, RoleId = agentRole.Id },
            new IdentityUserRole<Guid> { UserId = adminUser.Id, RoleId = adminRole.Id });

        var customerOneId = Guid.NewGuid();
        var customerTwoId = Guid.NewGuid();

        var categoryId = Guid.NewGuid();
        var priorityId = Guid.NewGuid();

        dbContext.TicketCategories.Add(new TicketCategory
        {
            Id = categoryId,
            Name = $"General-{Guid.NewGuid():N}",
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        dbContext.TicketPriorities.Add(new TicketPriority
        {
            Id = priorityId,
            Name = $"Normal-{Guid.NewGuid():N}",
            SortOrder = 20,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        dbContext.Customers.AddRange(
            new Customer { Id = customerOneId, Name = "Customer One", CreatedAtUtc = now, UpdatedAtUtc = now },
            new Customer { Id = customerTwoId, Name = "Customer Two", CreatedAtUtc = now, UpdatedAtUtc = now });

        var agentTicketId = Guid.NewGuid();
        var otherAgentTicketId = Guid.NewGuid();

        dbContext.Tickets.AddRange(
            new Ticket
            {
                Id = agentTicketId,
                CustomerId = customerOneId,
                AssignedToUserId = agentUserId,
                CategoryId = categoryId,
                PriorityId = priorityId,
                Subject = "Assigned to agent one",
                Description = "desc",
                Status = TicketStatus.InProgress,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new Ticket
            {
                Id = otherAgentTicketId,
                CustomerId = customerTwoId,
                AssignedToUserId = otherAgentUserId,
                CategoryId = categoryId,
                PriorityId = priorityId,
                Subject = "Assigned to agent two",
                Description = "desc",
                Status = TicketStatus.New,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

        await dbContext.SaveChangesAsync();

        return (agentTicketId, otherAgentTicketId);
    }

    private async Task<Guid> SeedTaskAsync(
        Guid ticketId,
        Guid createdByUserId,
        Guid assignedToUserId,
        string description,
        TicketTaskStatus status = TicketTaskStatus.Open)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        var now = DateTime.UtcNow;
        var task = new TicketTask
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            CreatedByUserId = createdByUserId,
            AssignedToUserId = assignedToUserId,
            Description = description,
            DueAtUtc = now.AddHours(6),
            Status = status,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CompletedAtUtc = status == TicketTaskStatus.Done ? now : null,
            CompletedByUserId = status == TicketTaskStatus.Done ? assignedToUserId : null
        };

        dbContext.TicketTasks.Add(task);
        await dbContext.SaveChangesAsync();
        return task.Id;
    }

    private async Task SetTaskRowVersionAsync(Guid taskId, byte[] rowVersion)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        var task = await dbContext.TicketTasks.FirstAsync(row => row.Id == taskId);
        task.RowVersion = rowVersion;
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
