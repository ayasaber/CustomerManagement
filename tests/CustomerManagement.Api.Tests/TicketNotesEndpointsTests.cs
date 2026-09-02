using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.TicketNotes;
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

public sealed class TicketNotesEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;
    private readonly CustomerManagementApiFactory _factory;

    public TicketNotesEndpointsTests(CustomerManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateAndListNote_WithMention_StoresNotification_AndKeepsAssignmentUntilHandoffAccepted()
    {
        var adminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var agentOneUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var agentTwoUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var seeded = await SeedScenarioAsync(adminUserId, agentOneUserId, agentTwoUserId);

        var createRequest = new CreateTicketInternalNoteRequest(
            seeded.AgentOneTicketId,
            "Investigated root cause and requesting peer review.",
            [agentTwoUserId],
            true,
            agentTwoUserId);

        using var createHttp = NewRoleRequest(HttpMethod.Post, "/api/ticket-notes", AuthRoles.Agent, agentOneUserId, createRequest);
        var createResponse = await _client.SendAsync(createHttp);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<TicketInternalNoteCreateResponse>();
        Assert.NotNull(created);
        Assert.Equal(agentOneUserId, created!.Note.AuthorUserId);
        Assert.Single(created.Note.Mentions);
        Assert.Equal(agentTwoUserId, created.Note.Mentions[0].MentionedUserId);
        Assert.NotNull(created.ReassignProposal);
        Assert.Equal(agentTwoUserId, created.ReassignProposal!.ReassignToUserId);

        using var listHttp = NewRoleRequest(
            HttpMethod.Get,
            $"/api/ticket-notes?ticketId={seeded.AgentOneTicketId}",
            AuthRoles.Agent,
            agentOneUserId);

        var listResponse = await _client.SendAsync(listHttp);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var listed = await listResponse.Content.ReadFromJsonAsync<TicketInternalNoteListResponse>();
        Assert.NotNull(listed);
        Assert.True(listed!.TotalCount >= 1);
        Assert.Contains(listed.Items, item => item.Id == created.Note.Id);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        var ticket = await dbContext.Tickets.SingleAsync(item => item.Id == seeded.AgentOneTicketId);
        Assert.Equal(agentOneUserId, ticket.AssignedToUserId);

        var mentionNotification = await dbContext.UserNotifications
            .SingleOrDefaultAsync(item => item.RecipientUserId == agentTwoUserId && item.Type == "ticket-note-mention");

        Assert.NotNull(mentionNotification);
    }

    [Fact]
    public async Task CreateNote_ReturnsBadRequest_WhenMentionsContainDuplicatesOrTooMany()
    {
        var adminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var agentOneUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var agentTwoUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var seeded = await SeedScenarioAsync(adminUserId, agentOneUserId, agentTwoUserId);

        var duplicateMentionsRequest = new CreateTicketInternalNoteRequest(
            seeded.AgentOneTicketId,
            "Duplicate mention check",
            [agentTwoUserId, agentTwoUserId],
            false,
            null);

        using var duplicateHttp = NewRoleRequest(HttpMethod.Post, "/api/ticket-notes", AuthRoles.Agent, agentOneUserId, duplicateMentionsRequest);
        var duplicateResponse = await _client.SendAsync(duplicateHttp);

        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);

        var tooManyMentions = Enumerable.Range(0, 11)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        var tooManyMentionsRequest = new CreateTicketInternalNoteRequest(
            seeded.AgentOneTicketId,
            "Too many mentions",
            tooManyMentions,
            false,
            null);

        using var tooManyHttp = NewRoleRequest(HttpMethod.Post, "/api/ticket-notes", AuthRoles.Agent, agentOneUserId, tooManyMentionsRequest);
        var tooManyResponse = await _client.SendAsync(tooManyHttp);

        Assert.Equal(HttpStatusCode.BadRequest, tooManyResponse.StatusCode);
    }

    [Fact]
    public async Task CreateHandoffRequest_DoesNotAutoAssign_AndCreatesNotification()
    {
        var adminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var agentOneUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var agentTwoUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var seeded = await SeedScenarioAsync(adminUserId, agentOneUserId, agentTwoUserId);
        await SetTicketRowVersionAsync(seeded.AgentOneTicketId, [5]);

        var noteId = await SeedNoteAsync(seeded.AgentOneTicketId, agentOneUserId, "Please take this ticket.");

        var createHandoffRequest = new CreateTicketHandoffRequest(
            noteId,
            seeded.AgentOneTicketId,
            agentTwoUserId,
            "Can you take over?",
            [5]);

        using var createHandoffHttp = NewRoleRequest(
            HttpMethod.Post,
            $"/api/ticket-notes/{noteId}/handoff-requests",
            AuthRoles.Agent,
            agentOneUserId,
            createHandoffRequest);

        var createHandoffResponse = await _client.SendAsync(createHandoffHttp);
        Assert.Equal(HttpStatusCode.Created, createHandoffResponse.StatusCode);

        var created = await createHandoffResponse.Content.ReadFromJsonAsync<TicketHandoffRequestResponse>();
        Assert.NotNull(created);
        Assert.Equal("Pending", created!.Status);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        var ticket = await dbContext.Tickets.SingleAsync(item => item.Id == seeded.AgentOneTicketId);
        Assert.Equal(agentOneUserId, ticket.AssignedToUserId);

        var notification = await dbContext.UserNotifications
            .SingleOrDefaultAsync(item => item.RecipientUserId == agentTwoUserId && item.Type == "ticket-handoff-request");

        Assert.NotNull(notification);

        var requestHistory = await dbContext.TicketHistoryEntries
            .AsNoTracking()
            .Where(item => item.TicketId == seeded.AgentOneTicketId && item.ActionType == "ticket.handoff.requested")
            .OrderByDescending(item => item.OccurredAtUtc)
            .FirstOrDefaultAsync();

        Assert.NotNull(requestHistory);
        Assert.Equal("targetAssigneeUserId", requestHistory!.FieldName);
        Assert.Equal(agentTwoUserId.ToString(), requestHistory.NewValue);
    }

    [Fact]
    public async Task RespondHandoffRequest_Accept_ByTarget_AssignsTicket()
    {
        var adminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var agentOneUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var agentTwoUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var seeded = await SeedScenarioAsync(adminUserId, agentOneUserId, agentTwoUserId);

        await SetTicketRowVersionAsync(seeded.AgentOneTicketId, [8]);
        var handoffId = await CreateHandoffAsync(seeded.AgentOneTicketId, agentOneUserId, agentTwoUserId, [8]);

        var respondRequest = new RespondTicketHandoffRequest(true, "I can take it", [8]);

        using var respondHttp = NewRoleRequest(
            HttpMethod.Post,
            $"/api/ticket-notes/handoff-requests/{handoffId}/respond",
            AuthRoles.Agent,
            agentTwoUserId,
            respondRequest);

        var respondResponse = await _client.SendAsync(respondHttp);
        Assert.Equal(HttpStatusCode.OK, respondResponse.StatusCode);

        var response = await respondResponse.Content.ReadFromJsonAsync<TicketHandoffRequestResponse>();
        Assert.NotNull(response);
        Assert.Equal("Accepted", response!.Status);
        Assert.Equal(agentTwoUserId, response.UpdatedTicketAssigneeUserId);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();
        var ticket = await dbContext.Tickets.SingleAsync(item => item.Id == seeded.AgentOneTicketId);
        Assert.Equal(agentTwoUserId, ticket.AssignedToUserId);

        var responseHistory = await dbContext.TicketHistoryEntries
            .AsNoTracking()
            .Where(item => item.TicketId == seeded.AgentOneTicketId && item.ActionType == "ticket.handoff.responded")
            .OrderByDescending(item => item.OccurredAtUtc)
            .FirstOrDefaultAsync();

        Assert.NotNull(responseHistory);
        Assert.Equal("Accepted", responseHistory!.NewValue);

        var assignmentHistory = await dbContext.TicketHistoryEntries
            .AsNoTracking()
            .Where(item => item.TicketId == seeded.AgentOneTicketId && item.ActionType == "ticket.assigned")
            .OrderByDescending(item => item.OccurredAtUtc)
            .FirstOrDefaultAsync();

        Assert.NotNull(assignmentHistory);
        Assert.Equal(agentTwoUserId.ToString(), assignmentHistory!.NewValue);
    }

    [Fact]
    public async Task RespondHandoffRequest_Reject_ByTarget_DoesNotAssignTicket()
    {
        var adminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var agentOneUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var agentTwoUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var seeded = await SeedScenarioAsync(adminUserId, agentOneUserId, agentTwoUserId);

        await SetTicketRowVersionAsync(seeded.AgentOneTicketId, [9]);
        var handoffId = await CreateHandoffAsync(seeded.AgentOneTicketId, agentOneUserId, agentTwoUserId, [9]);

        var respondRequest = new RespondTicketHandoffRequest(false, "Cannot take it now", [9]);

        using var respondHttp = NewRoleRequest(
            HttpMethod.Post,
            $"/api/ticket-notes/handoff-requests/{handoffId}/respond",
            AuthRoles.Agent,
            agentTwoUserId,
            respondRequest);

        var respondResponse = await _client.SendAsync(respondHttp);
        Assert.Equal(HttpStatusCode.OK, respondResponse.StatusCode);

        var response = await respondResponse.Content.ReadFromJsonAsync<TicketHandoffRequestResponse>();
        Assert.NotNull(response);
        Assert.Equal("Rejected", response!.Status);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();
        var ticket = await dbContext.Tickets.SingleAsync(item => item.Id == seeded.AgentOneTicketId);
        Assert.Equal(agentOneUserId, ticket.AssignedToUserId);

        var responseHistory = await dbContext.TicketHistoryEntries
            .AsNoTracking()
            .Where(item => item.TicketId == seeded.AgentOneTicketId && item.ActionType == "ticket.handoff.responded")
            .OrderByDescending(item => item.OccurredAtUtc)
            .FirstOrDefaultAsync();

        Assert.NotNull(responseHistory);
        Assert.Equal("Rejected", responseHistory!.NewValue);
    }

    [Fact]
    public async Task RespondHandoffRequest_Accept_ByAdminOverride_AssignsTicket()
    {
        var adminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var agentOneUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var agentTwoUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var seeded = await SeedScenarioAsync(adminUserId, agentOneUserId, agentTwoUserId);

        await SetTicketRowVersionAsync(seeded.AgentOneTicketId, [10]);
        var handoffId = await CreateHandoffAsync(seeded.AgentOneTicketId, agentOneUserId, agentTwoUserId, [10]);

        var respondRequest = new RespondTicketHandoffRequest(true, "Admin override accepted", [10]);

        using var respondHttp = NewRoleRequest(
            HttpMethod.Post,
            $"/api/ticket-notes/handoff-requests/{handoffId}/respond",
            AuthRoles.Admin,
            adminUserId,
            respondRequest);

        var respondResponse = await _client.SendAsync(respondHttp);
        Assert.Equal(HttpStatusCode.OK, respondResponse.StatusCode);

        var response = await respondResponse.Content.ReadFromJsonAsync<TicketHandoffRequestResponse>();
        Assert.NotNull(response);
        Assert.Equal("Accepted", response!.Status);
        Assert.Equal(agentTwoUserId, response.UpdatedTicketAssigneeUserId);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();
        var ticket = await dbContext.Tickets.SingleAsync(item => item.Id == seeded.AgentOneTicketId);
        Assert.Equal(agentTwoUserId, ticket.AssignedToUserId);
    }

    private async Task<(Guid AgentOneTicketId, Guid AgentTwoTicketId)> SeedScenarioAsync(Guid adminUserId, Guid agentOneUserId, Guid agentTwoUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        dbContext.UserNotifications.RemoveRange(dbContext.UserNotifications);
        dbContext.TicketHandoffRequests.RemoveRange(dbContext.TicketHandoffRequests);
        dbContext.TicketInternalNoteMentions.RemoveRange(dbContext.TicketInternalNoteMentions);
        dbContext.TicketInternalNotes.RemoveRange(dbContext.TicketInternalNotes);
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

        var adminUser = NewUser(adminUserId, "Admin User", now);
        var agentOneUser = NewUser(agentOneUserId, "Agent One", now);
        var agentTwoUser = NewUser(agentTwoUserId, "Agent Two", now);

        dbContext.Users.AddRange(adminUser, agentOneUser, agentTwoUser);
        dbContext.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = adminUserId, RoleId = adminRole.Id },
            new IdentityUserRole<Guid> { UserId = agentOneUserId, RoleId = agentRole.Id },
            new IdentityUserRole<Guid> { UserId = agentTwoUserId, RoleId = agentRole.Id });

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

        var agentOneTicketId = Guid.NewGuid();
        var agentTwoTicketId = Guid.NewGuid();

        dbContext.Tickets.AddRange(
            new Ticket
            {
                Id = agentOneTicketId,
                CustomerId = customerOneId,
                AssignedToUserId = agentOneUserId,
                CategoryId = categoryId,
                PriorityId = priorityId,
                Subject = "Assigned to agent one",
                Description = "desc",
                Status = TicketStatus.InProgress,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                RowVersion = [1]
            },
            new Ticket
            {
                Id = agentTwoTicketId,
                CustomerId = customerTwoId,
                AssignedToUserId = agentTwoUserId,
                CategoryId = categoryId,
                PriorityId = priorityId,
                Subject = "Assigned to agent two",
                Description = "desc",
                Status = TicketStatus.New,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                RowVersion = [1]
            });

        await dbContext.SaveChangesAsync();

        return (agentOneTicketId, agentTwoTicketId);
    }

    private async Task<Guid> SeedNoteAsync(Guid ticketId, Guid authorUserId, string body)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        var now = DateTime.UtcNow;
        var note = new TicketInternalNote
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorUserId = authorUserId,
            Body = body,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            RowVersion = [1]
        };

        dbContext.TicketInternalNotes.Add(note);
        await dbContext.SaveChangesAsync();
        return note.Id;
    }

    private async Task<Guid> CreateHandoffAsync(Guid ticketId, Guid requesterUserId, Guid targetUserId, byte[] ticketRowVersion)
    {
        var noteId = await SeedNoteAsync(ticketId, requesterUserId, "Need reassignment");

        var request = new CreateTicketHandoffRequest(noteId, ticketId, targetUserId, "Please take this.", ticketRowVersion);

        using var http = NewRoleRequest(
            HttpMethod.Post,
            $"/api/ticket-notes/{noteId}/handoff-requests",
            AuthRoles.Agent,
            requesterUserId,
            request);

        var response = await _client.SendAsync(http);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TicketHandoffRequestResponse>();
        Assert.NotNull(created);
        return created!.Id;
    }

    private async Task SetTicketRowVersionAsync(Guid ticketId, byte[] rowVersion)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        var ticket = await dbContext.Tickets.SingleAsync(item => item.Id == ticketId);
        ticket.RowVersion = rowVersion;
        await dbContext.SaveChangesAsync();
    }

    private static ApplicationUser NewUser(Guid userId, string displayName, DateTime now)
    {
        var idText = userId.ToString("N");
        var email = $"user-{idText}@crm.local";

        return new ApplicationUser
        {
            Id = userId,
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
}
