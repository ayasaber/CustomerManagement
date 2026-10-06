using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.TicketMessages;
using CustomerManagement.Api.Contracts.Tickets;
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

public sealed class TicketMessagesEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;
    private readonly CustomerManagementApiFactory _factory;

    public TicketMessagesEndpointsTests(CustomerManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task List_IncludesTicketDescriptionAsFirstChronologicalComment()
    {
        var seeded = await SeedScenarioAsync();
        await SeedMessageAsync(seeded.AgentTicketId, seeded.AgentUserId, "Follow-up update", DateTime.UtcNow.AddMinutes(1));

        using var request = NewRoleRequest(
            HttpMethod.Get,
            $"/api/ticket-messages?ticketId={seeded.AgentTicketId}&page=1&pageSize=20",
            AuthRoles.Agent,
            seeded.AgentUserId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TicketMessageListResponse>();
        Assert.NotNull(payload);
        Assert.True(payload!.TotalCount >= 2);
        Assert.Equal("Initial ticket description", payload.Items[0].Body);
        Assert.Equal("customer", payload.Items[0].SenderType);
    }

    [Fact]
    public async Task AgentCanPostMessage_OnAssignedTicket_AndHistoryEntryIsWritten()
    {
        var seeded = await SeedScenarioAsync();

        var createRequest = new CreateTicketMessageRequest(seeded.AgentTicketId, "I am working on this now.");
        using var request = NewRoleRequest(HttpMethod.Post, "/api/ticket-messages", AuthRoles.Agent, seeded.AgentUserId, createRequest);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CreateTicketMessageResponse>();
        Assert.NotNull(payload);
        Assert.Equal("agent", payload!.Message.SenderType);
        Assert.Equal(seeded.AgentUserId, payload.Message.SenderUserId);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        var history = await dbContext.TicketHistoryEntries
            .AsNoTracking()
            .Where(row => row.TicketId == seeded.AgentTicketId && row.ActionType == "ticket.message.posted")
            .OrderByDescending(row => row.OccurredAtUtc)
            .FirstOrDefaultAsync();

        Assert.NotNull(history);
        Assert.Equal("messageId", history!.FieldName);
        Assert.Equal(payload.Message.Id.ToString(), history.NewValue);
    }

    [Fact]
    public async Task AgentIsForbidden_WhenPostingToTicketAssignedToAnotherAgent()
    {
        var seeded = await SeedScenarioAsync();

        var createRequest = new CreateTicketMessageRequest(seeded.OtherAgentTicketId, "Attempted post.");
        using var request = NewRoleRequest(HttpMethod.Post, "/api/ticket-messages", AuthRoles.Agent, seeded.AgentUserId, createRequest);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CustomerCanReadOwnedTicketMessages_ButCannotReadNonOwnedTicketMessages()
    {
        var seeded = await SeedScenarioAsync();
        await SeedMessageAsync(seeded.AgentTicketId, seeded.AgentUserId, "Customer-visible update", DateTime.UtcNow.AddMinutes(2));

        using var ownedRequest = NewRoleRequest(
            HttpMethod.Get,
            $"/api/ticket-messages?ticketId={seeded.AgentTicketId}&page=1&pageSize=10",
            AuthRoles.Customer,
            seeded.CustomerOneUserId);

        var ownedResponse = await _client.SendAsync(ownedRequest);
        Assert.Equal(HttpStatusCode.OK, ownedResponse.StatusCode);

        using var nonOwnedRequest = NewRoleRequest(
            HttpMethod.Get,
            $"/api/ticket-messages?ticketId={seeded.OtherAgentTicketId}&page=1&pageSize=10",
            AuthRoles.Customer,
            seeded.CustomerOneUserId);

        var nonOwnedResponse = await _client.SendAsync(nonOwnedRequest);
        Assert.Equal(HttpStatusCode.NotFound, nonOwnedResponse.StatusCode);
    }

    [Fact]
    public async Task CustomerCanPostMessage_OnOwnOpenTicket_SenderTypeIsCustomer()
    {
        var seeded = await SeedScenarioAsync();

        var createRequest = new CreateTicketMessageRequest(seeded.AgentTicketId, "Here is more detail on my issue.");
        using var request = NewRoleRequest(HttpMethod.Post, "/api/ticket-messages", AuthRoles.Customer, seeded.CustomerOneUserId, createRequest);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CreateTicketMessageResponse>();
        Assert.NotNull(payload);
        Assert.Equal("customer", payload!.Message.SenderType);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();
        var ticket = await dbContext.Tickets.AsNoTracking().FirstAsync(row => row.Id == seeded.AgentTicketId);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
    }

    [Fact]
    public async Task CustomerReply_OnResolvedTicket_AutoReopensToInProgress()
    {
        var seeded = await SeedScenarioAsync();
        await SetTicketStatusAsync(seeded.AgentTicketId, TicketStatus.Resolved);

        var createRequest = new CreateTicketMessageRequest(seeded.AgentTicketId, "I am still seeing the issue.");
        using var request = NewRoleRequest(HttpMethod.Post, "/api/ticket-messages", AuthRoles.Customer, seeded.CustomerOneUserId, createRequest);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var getRequest = NewRoleRequest(HttpMethod.Get, $"/api/tickets/{seeded.AgentTicketId}", AuthRoles.Customer, seeded.CustomerOneUserId);
        var getResponse = await _client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var ticket = await getResponse.Content.ReadFromJsonAsync<TicketResponse>();
        Assert.NotNull(ticket);
        Assert.Equal("in_progress", ticket!.Status);
    }

    [Fact]
    public async Task CustomerReply_OnWaitingOnCustomerTicket_AutoReopensToInProgress()
    {
        var seeded = await SeedScenarioAsync();
        await SetTicketStatusAsync(seeded.AgentTicketId, TicketStatus.WaitingOnCustomer);

        var createRequest = new CreateTicketMessageRequest(seeded.AgentTicketId, "Here is the information you asked for.");
        using var request = NewRoleRequest(HttpMethod.Post, "/api/ticket-messages", AuthRoles.Customer, seeded.CustomerOneUserId, createRequest);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var getRequest = NewRoleRequest(HttpMethod.Get, $"/api/tickets/{seeded.AgentTicketId}", AuthRoles.Customer, seeded.CustomerOneUserId);
        var getResponse = await _client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var ticket = await getResponse.Content.ReadFromJsonAsync<TicketResponse>();
        Assert.NotNull(ticket);
        Assert.Equal("in_progress", ticket!.Status);
    }

    [Fact]
    public async Task CustomerReply_OnClosedTicket_ReturnsBadRequest_AndDoesNotPersistMessage()
    {
        var seeded = await SeedScenarioAsync();
        await SetTicketStatusAsync(seeded.AgentTicketId, TicketStatus.Closed);

        var createRequest = new CreateTicketMessageRequest(seeded.AgentTicketId, "Trying to reply anyway.");
        using var request = NewRoleRequest(HttpMethod.Post, "/api/ticket-messages", AuthRoles.Customer, seeded.CustomerOneUserId, createRequest);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        var ticket = await dbContext.Tickets.AsNoTracking().FirstAsync(row => row.Id == seeded.AgentTicketId);
        Assert.Equal(TicketStatus.Closed, ticket.Status);

        var messageCount = await dbContext.TicketMessages.CountAsync(row => row.TicketId == seeded.AgentTicketId);
        Assert.Equal(0, messageCount);
    }

    [Fact]
    public async Task CustomerReply_OnAnotherCustomersTicket_ReturnsNotFound()
    {
        var seeded = await SeedScenarioAsync();

        var createRequest = new CreateTicketMessageRequest(seeded.OtherAgentTicketId, "Trying to reply to someone else's ticket.");
        using var request = NewRoleRequest(HttpMethod.Post, "/api/ticket-messages", AuthRoles.Customer, seeded.CustomerOneUserId, createRequest);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InternalNotesAreNotReturned_FromMessageEndpoint()
    {
        var seeded = await SeedScenarioAsync();

        await SeedInternalNoteAsync(seeded.AgentTicketId, seeded.AgentUserId, "Internal only note");

        using var request = NewRoleRequest(
            HttpMethod.Get,
            $"/api/ticket-messages?ticketId={seeded.AgentTicketId}&page=1&pageSize=10",
            AuthRoles.Agent,
            seeded.AgentUserId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TicketMessageListResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.TotalCount);
        Assert.Single(payload.Items);
        Assert.Equal("Initial ticket description", payload.Items[0].Body);
    }

    private async Task SetTicketStatusAsync(Guid ticketId, TicketStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        var ticket = await dbContext.Tickets.FirstAsync(row => row.Id == ticketId);
        ticket.Status = status;
        await dbContext.SaveChangesAsync();
    }

    private async Task<SeededScenario> SeedScenarioAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        dbContext.TicketMessages.RemoveRange(dbContext.TicketMessages);
        dbContext.TicketInternalNoteMentions.RemoveRange(dbContext.TicketInternalNoteMentions);
        dbContext.TicketInternalNotes.RemoveRange(dbContext.TicketInternalNotes);
        dbContext.TicketHandoffRequests.RemoveRange(dbContext.TicketHandoffRequests);
        dbContext.UserNotifications.RemoveRange(dbContext.UserNotifications);
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
        var customerRole = await dbContext.Roles.AsNoTracking().FirstAsync(role => role.Name == AuthRoles.Customer);

        var adminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var agentUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var otherAgentUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var customerOneUserId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var customerTwoUserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        var users = new[]
        {
            NewUser(adminUserId, "Admin User", "admin-user@crm.local", now),
            NewUser(agentUserId, "Agent One", "agent-one@crm.local", now),
            NewUser(otherAgentUserId, "Agent Two", "agent-two@crm.local", now),
            NewUser(customerOneUserId, "Customer One", "customer-one@crm.local", now),
            NewUser(customerTwoUserId, "Customer Two", "customer-two@crm.local", now)
        };

        dbContext.Users.AddRange(users);
        dbContext.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = adminUserId, RoleId = adminRole.Id },
            new IdentityUserRole<Guid> { UserId = agentUserId, RoleId = agentRole.Id },
            new IdentityUserRole<Guid> { UserId = otherAgentUserId, RoleId = agentRole.Id },
            new IdentityUserRole<Guid> { UserId = customerOneUserId, RoleId = customerRole.Id },
            new IdentityUserRole<Guid> { UserId = customerTwoUserId, RoleId = customerRole.Id });

        var customerOneId = Guid.NewGuid();
        var customerTwoId = Guid.NewGuid();

        dbContext.Customers.AddRange(
            new Customer
            {
                Id = customerOneId,
                ApplicationUserId = customerOneUserId,
                Name = "Customer One",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new Customer
            {
                Id = customerTwoId,
                ApplicationUserId = customerTwoUserId,
                Name = "Customer Two",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

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
                Subject = "Agent ticket",
                Description = "Initial ticket description",
                Status = TicketStatus.InProgress,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                RowVersion = [1]
            },
            new Ticket
            {
                Id = otherAgentTicketId,
                CustomerId = customerTwoId,
                AssignedToUserId = otherAgentUserId,
                CategoryId = categoryId,
                PriorityId = priorityId,
                Subject = "Other agent ticket",
                Description = "Initial ticket description for other ticket",
                Status = TicketStatus.New,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                RowVersion = [1]
            });

        dbContext.TicketHistoryEntries.AddRange(
            new TicketHistoryEntry
            {
                Id = Guid.NewGuid(),
                TicketId = agentTicketId,
                ActionType = "ticket.created",
                FieldName = "status",
                NewValue = "new",
                ActorUserId = customerOneUserId,
                ActorEmail = "customer-one@crm.local",
                OccurredAtUtc = now
            },
            new TicketHistoryEntry
            {
                Id = Guid.NewGuid(),
                TicketId = otherAgentTicketId,
                ActionType = "ticket.created",
                FieldName = "status",
                NewValue = "new",
                ActorUserId = customerTwoUserId,
                ActorEmail = "customer-two@crm.local",
                OccurredAtUtc = now
            });

        await dbContext.SaveChangesAsync();

        return new SeededScenario(
            agentTicketId,
            otherAgentTicketId,
            agentUserId,
            otherAgentUserId,
            customerOneUserId,
            customerTwoUserId);
    }

    private async Task SeedMessageAsync(Guid ticketId, Guid senderUserId, string body, DateTime createdAtUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        dbContext.TicketMessages.Add(new TicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            SenderType = TicketMessageSenderType.Agent,
            SenderUserId = senderUserId,
            SenderDisplayName = "Agent One",
            Body = body,
            CreatedAtUtc = createdAtUtc,
            RowVersion = [1]
        });

        await dbContext.SaveChangesAsync();
    }

    private async Task SeedInternalNoteAsync(Guid ticketId, Guid authorUserId, string body)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();
        var now = DateTime.UtcNow;

        dbContext.TicketInternalNotes.Add(new TicketInternalNote
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorUserId = authorUserId,
            Body = body,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            RowVersion = [1]
        });

        await dbContext.SaveChangesAsync();
    }

    private static ApplicationUser NewUser(Guid id, string displayName, string email, DateTime now)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = email,
            Email = email,
            NormalizedUserName = email.ToUpperInvariant(),
            NormalizedEmail = email.ToUpperInvariant(),
            DisplayName = displayName,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
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

    private sealed record SeededScenario(
        Guid AgentTicketId,
        Guid OtherAgentTicketId,
        Guid AgentUserId,
        Guid OtherAgentUserId,
        Guid CustomerOneUserId,
        Guid CustomerTwoUserId);
}
