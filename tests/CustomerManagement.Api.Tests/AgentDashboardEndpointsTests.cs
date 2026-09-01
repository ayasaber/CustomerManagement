using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.Dashboard;
using CustomerManagement.Api.Domain.Customers;
using CustomerManagement.Api.Domain.Tickets;
using CustomerManagement.Api.Infrastructure.Persistence;
using CustomerManagement.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerManagement.Api.Tests;

public sealed class AgentDashboardEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;
    private readonly CustomerManagementApiFactory _factory;

    public AgentDashboardEndpointsTests(CustomerManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AssignedTickets_AgentSeesOnlyOwnAssignments()
    {
        var agentUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var otherAgentUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var seeded = await SeedAssignedTicketScenarioAsync(agentUserId, otherAgentUserId);

        using var request = NewRoleRequest(
            HttpMethod.Get,
            "/api/dashboard/me/assigned-tickets?page=1&pageSize=20",
            "agent",
            agentUserId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<DashboardAssignedTicketsResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.TotalCount);
        Assert.Single(payload.Items);
        Assert.Equal(seeded.AgentTicketId, payload.Items[0].TicketId);
        Assert.Equal(seeded.CustomerOneName, payload.Items[0].CustomerDisplayName);
    }

    [Fact]
    public async Task AssignedTickets_AdminGetsAllByDefault_AndCanFilterToSelf()
    {
        var adminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var agentUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var otherAgentUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        await SeedAssignedTicketScenarioAsync(agentUserId, otherAgentUserId, adminUserId);

        using var allRequest = NewRoleRequest(
            HttpMethod.Get,
            "/api/dashboard/me/assigned-tickets?page=1&pageSize=20",
            "admin",
            adminUserId);

        var allResponse = await _client.SendAsync(allRequest);
        Assert.Equal(HttpStatusCode.OK, allResponse.StatusCode);
        var allPayload = await allResponse.Content.ReadFromJsonAsync<DashboardAssignedTicketsResponse>();
        Assert.NotNull(allPayload);
        Assert.Equal(3, allPayload!.TotalCount);

        using var mineRequest = NewRoleRequest(
            HttpMethod.Get,
            "/api/dashboard/me/assigned-tickets?assignedToMeOnly=true&page=1&pageSize=20",
            "admin",
            adminUserId);

        var mineResponse = await _client.SendAsync(mineRequest);
        Assert.Equal(HttpStatusCode.OK, mineResponse.StatusCode);
        var minePayload = await mineResponse.Content.ReadFromJsonAsync<DashboardAssignedTicketsResponse>();
        Assert.NotNull(minePayload);
        Assert.Equal(1, minePayload!.TotalCount);
    }

    [Fact]
    public async Task OpenTasks_ReturnsEmptyPlaceholderResponse()
    {
        var agentUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        using var request = NewRoleRequest(
            HttpMethod.Get,
            "/api/dashboard/me/open-tasks?page=1&pageSize=20",
            "agent",
            agentUserId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<DashboardOpenTaskSummaryResponse>();
        Assert.NotNull(payload);
        Assert.Equal(0, payload!.TotalCount);
        Assert.Empty(payload.Items);
    }

    [Fact]
    public async Task CustomerContext_ReturnsContextAndRecentInteractions_ForAssignedAgent()
    {
        var agentUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var otherAgentUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var seeded = await SeedAssignedTicketScenarioAsync(agentUserId, otherAgentUserId);

        using var request = NewRoleRequest(
            HttpMethod.Get,
            $"/api/dashboard/tickets/{seeded.AgentTicketId}/customer-context",
            "agent",
            agentUserId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<DashboardCustomerContextResponse>();
        Assert.NotNull(payload);
        Assert.Equal(seeded.AgentTicketId, payload!.TicketId);
        Assert.Equal(seeded.CustomerOneName, payload.CustomerDisplayName);
        Assert.Equal(seeded.CustomerOneEmail, payload.PrimaryEmail);
        Assert.Equal(seeded.CustomerOnePhone, payload.PrimaryPhone);
        Assert.Equal(2, payload.RecentInteractions.Count);
        Assert.Equal("whatsapp", payload.RecentInteractions[0].Type);
        Assert.Equal("new interaction", payload.RecentInteractions[0].Summary);
    }

    [Fact]
    public async Task CustomerContext_ReturnsNotFound_WhenAgentIsNotTicketAssignee()
    {
        var agentUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var otherAgentUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var seeded = await SeedAssignedTicketScenarioAsync(agentUserId, otherAgentUserId);

        using var request = NewRoleRequest(
            HttpMethod.Get,
            $"/api/dashboard/tickets/{seeded.OtherAgentTicketId}/customer-context",
            "agent",
            agentUserId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignedTickets_ReturnsUnauthorized_WhenRoleHeaderMissing()
    {
        var response = await _client.GetAsync("/api/dashboard/me/assigned-tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<(Guid AgentTicketId, Guid OtherAgentTicketId, string CustomerOneName, string CustomerOneEmail, string CustomerOnePhone)> SeedAssignedTicketScenarioAsync(
        Guid agentUserId,
        Guid otherAgentUserId,
        Guid? adminUserId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        dbContext.TicketHistoryEntries.RemoveRange(dbContext.TicketHistoryEntries);
        dbContext.Tickets.RemoveRange(dbContext.Tickets);
        dbContext.CustomerInteractionEvents.RemoveRange(dbContext.CustomerInteractionEvents);
        dbContext.ContactDetails.RemoveRange(dbContext.ContactDetails);
        dbContext.Customers.RemoveRange(dbContext.Customers);
        dbContext.TicketPriorities.RemoveRange(dbContext.TicketPriorities);
        dbContext.TicketCategories.RemoveRange(dbContext.TicketCategories);
        await dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;

        var customerOneId = Guid.NewGuid();
        var customerTwoId = Guid.NewGuid();
        var customerThreeId = Guid.NewGuid();

        var categoryId = Guid.NewGuid();
        var priorityId = Guid.NewGuid();

        dbContext.TicketCategories.Add(new TicketCategory
        {
            Id = categoryId,
            Name = $"General-{Guid.NewGuid():N}",
            Description = "General",
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        dbContext.TicketPriorities.Add(new TicketPriority
        {
            Id = priorityId,
            Name = $"Normal-{Guid.NewGuid():N}",
            SortOrder = 10,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        dbContext.Customers.AddRange(
            new Customer
            {
                Id = customerOneId,
                Name = "Customer One",
                Company = "Contoso",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new Customer
            {
                Id = customerTwoId,
                Name = "Customer Two",
                Company = "Fabrikam",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new Customer
            {
                Id = customerThreeId,
                Name = "Customer Three",
                Company = "Wingtip",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

        var customerOneEmail = "customer.one@crm.local";
        var customerOnePhone = "+201000000001";

        dbContext.ContactDetails.AddRange(
            new ContactDetail
            {
                Id = Guid.NewGuid(),
                CustomerId = customerOneId,
                Channel = ContactChannel.Email,
                Value = customerOneEmail,
                Label = "work",
                IsPrimary = true,
                CreatedAtUtc = now
            },
            new ContactDetail
            {
                Id = Guid.NewGuid(),
                CustomerId = customerOneId,
                Channel = ContactChannel.Phone,
                Value = customerOnePhone,
                Label = "primary",
                IsPrimary = true,
                CreatedAtUtc = now
            });

        dbContext.CustomerInteractionEvents.AddRange(
            new CustomerInteractionEvent
            {
                Id = Guid.NewGuid(),
                CustomerId = customerOneId,
                Channel = InteractionChannel.Email,
                Direction = InteractionDirection.Inbound,
                OccurredAtUtc = now.AddMinutes(-30),
                Summary = "old interaction",
                SourceRef = "src-old",
                SourceSystem = "channels",
                ProjectedAtUtc = now
            },
            new CustomerInteractionEvent
            {
                Id = Guid.NewGuid(),
                CustomerId = customerOneId,
                Channel = InteractionChannel.WhatsApp,
                Direction = InteractionDirection.Outbound,
                OccurredAtUtc = now.AddMinutes(-5),
                Summary = "new interaction",
                SourceRef = "src-new",
                SourceSystem = "channels",
                ProjectedAtUtc = now
            });

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
                Subject = "Assigned to first agent",
                Description = "desc",
                Status = TicketStatus.InProgress,
                IsEscalated = false,
                CreatedAtUtc = now.AddMinutes(-15),
                UpdatedAtUtc = now.AddMinutes(-2)
            },
            new Ticket
            {
                Id = otherAgentTicketId,
                CustomerId = customerTwoId,
                AssignedToUserId = otherAgentUserId,
                CategoryId = categoryId,
                PriorityId = priorityId,
                Subject = "Assigned to second agent",
                Description = "desc",
                Status = TicketStatus.New,
                IsEscalated = false,
                CreatedAtUtc = now.AddMinutes(-20),
                UpdatedAtUtc = now.AddMinutes(-3)
            });

        if (adminUserId.HasValue)
        {
            dbContext.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                CustomerId = customerThreeId,
                AssignedToUserId = adminUserId.Value,
                CategoryId = categoryId,
                PriorityId = priorityId,
                Subject = "Assigned to admin",
                Description = "desc",
                Status = TicketStatus.WaitingOnCustomer,
                IsEscalated = true,
                CreatedAtUtc = now.AddMinutes(-25),
                UpdatedAtUtc = now.AddMinutes(-1)
            });
        }

        await dbContext.SaveChangesAsync();

        return (agentTicketId, otherAgentTicketId, "Customer One", customerOneEmail, customerOnePhone);
    }

    private static HttpRequestMessage NewRoleRequest(HttpMethod method, string uri, string role, Guid userId)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(TestAuthDefaults.RoleHeader, role);
        request.Headers.Add(TestAuthDefaults.UserIdHeader, userId.ToString());
        return request;
    }
}
