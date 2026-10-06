using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.Auth;
using CustomerManagement.Api.Contracts.Customers;
using CustomerManagement.Api.Contracts.Tickets;
using CustomerManagement.Api.Domain.Security;
using CustomerManagement.Api.Domain.Tickets;
using CustomerManagement.Api.Infrastructure.Attachments;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using CustomerManagement.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CustomerManagement.Api.Tests;

public sealed class TicketAttachmentEndpointsTests : IClassFixture<CustomerManagementApiFactory>, IAsyncLifetime
{
    private static readonly Guid AgentOneUserId = Guid.Parse("e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1");
    private static readonly Guid AgentTwoUserId = Guid.Parse("e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2");
    private static readonly Guid AdminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly HttpClient _client;
    private readonly CustomerManagementApiFactory _factory;

    public TicketAttachmentEndpointsTests(CustomerManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        await EnsureUserSeededAsync(dbContext, AgentOneUserId, "Agent One", "ticket-attach-agent-one@crm.local", AuthRoles.Agent);
        await EnsureUserSeededAsync(dbContext, AgentTwoUserId, "Agent Two", "ticket-attach-agent-two@crm.local", AuthRoles.Agent);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task EnsureUserSeededAsync(
        CustomerManagementDbContext dbContext,
        Guid userId,
        string displayName,
        string email,
        string roleName)
    {
        var exists = await dbContext.Users.AnyAsync(row => row.Id == userId);
        if (exists)
        {
            return;
        }

        var now = DateTime.UtcNow;
        dbContext.Users.Add(new ApplicationUser
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
        });

        var role = await dbContext.Roles.AsNoTracking().FirstAsync(row => row.Name == roleName);
        dbContext.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = role.Id });

        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task UploadAttachment_ReturnsCreated_WhenCustomerUploadsToOwnTicket()
    {
        var customer = await CreateCustomerAsync("Attach Customer One", "Contoso");
        var category = await CreateCategoryAsync($"General-{Guid.NewGuid():N}");
        var priority = await CreatePriorityAsync($"Normal-{Guid.NewGuid():N}", 20);
        var ticket = await CreateTicketAsync(customer, category, priority, "Need help", "Description text.");

        using var request = NewMultipartRequest(
            HttpMethod.Post,
            $"/api/tickets/{ticket.Id}/attachments",
            AuthRoles.Customer,
            customer.ApplicationUserId!.Value,
            "evidence.txt",
            "text/plain",
            "attachment content");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<TicketAttachmentResponse>();
        Assert.NotNull(payload);
        Assert.Equal(ticket.Id, payload!.TicketId);
        Assert.Equal("evidence.txt", payload.OriginalFileName);
        Assert.Equal("text/plain", payload.ContentType);
        Assert.True(payload.SizeBytes > 0);
    }

    [Fact]
    public async Task UploadAttachment_ReturnsNotFound_WhenTicketBelongsToAnotherCustomer()
    {
        var owner = await CreateCustomerAsync("Attach Owner", "Contoso");
        var otherCustomer = await CreateCustomerAsync("Attach Other", "Fabrikam");
        var category = await CreateCategoryAsync($"General-{Guid.NewGuid():N}");
        var priority = await CreatePriorityAsync($"Normal-{Guid.NewGuid():N}", 20);
        var ticket = await CreateTicketAsync(owner, category, priority, "Owner ticket", "Owner description.");

        using var request = NewMultipartRequest(
            HttpMethod.Post,
            $"/api/tickets/{ticket.Id}/attachments",
            AuthRoles.Customer,
            otherCustomer.ApplicationUserId!.Value,
            "evidence.txt",
            "text/plain",
            "attempted upload");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadAttachment_ReturnsForbidden_WhenAgentUploadsToTicketAssignedToAnotherAgent()
    {
        var customer = await CreateCustomerAsync("Attach Assign Customer", "Contoso");
        var category = await CreateCategoryAsync($"General-{Guid.NewGuid():N}");
        var priority = await CreatePriorityAsync($"Normal-{Guid.NewGuid():N}", 20);
        var ticket = await CreateTicketAsync(customer, category, priority, "Assigned ticket", "Needs an agent.");

        await AssignTicketAsync(ticket, AgentOneUserId);

        using var request = NewMultipartRequest(
            HttpMethod.Post,
            $"/api/tickets/{ticket.Id}/attachments",
            AuthRoles.Agent,
            AgentTwoUserId,
            "note.txt",
            "text/plain",
            "attempted upload by other agent");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadAttachment_ReturnsCreated_WhenAgentUploadsToUnassignedTicket()
    {
        var customer = await CreateCustomerAsync("Attach Unassigned Customer", "Contoso");
        var category = await CreateCategoryAsync($"General-{Guid.NewGuid():N}");
        var priority = await CreatePriorityAsync($"Normal-{Guid.NewGuid():N}", 20);
        var ticket = await CreateTicketAsync(customer, category, priority, "Unassigned ticket", "No agent yet.");

        using var request = NewMultipartRequest(
            HttpMethod.Post,
            $"/api/tickets/{ticket.Id}/attachments",
            AuthRoles.Agent,
            AgentOneUserId,
            "note.txt",
            "text/plain",
            "agent upload on unassigned ticket");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task UploadAttachment_ReturnsBadRequest_WhenContentTypeIsNotAllowed()
    {
        var customer = await CreateCustomerAsync("Attach ContentType Customer", "Contoso");
        var category = await CreateCategoryAsync($"General-{Guid.NewGuid():N}");
        var priority = await CreatePriorityAsync($"Normal-{Guid.NewGuid():N}", 20);
        var ticket = await CreateTicketAsync(customer, category, priority, "Bad content type", "Attempt disallowed type.");

        using var request = NewMultipartRequest(
            HttpMethod.Post,
            $"/api/tickets/{ticket.Id}/attachments",
            AuthRoles.Customer,
            customer.ApplicationUserId!.Value,
            "malware.exe",
            "application/octet-stream",
            "not allowed type");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("contentType", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAttachment_ReturnsBadRequest_WhenFileTooLarge()
    {
        var customer = await CreateCustomerAsync("Attach TooLarge Customer", "Contoso");
        var category = await CreateCategoryAsync($"General-{Guid.NewGuid():N}");
        var priority = await CreatePriorityAsync($"Normal-{Guid.NewGuid():N}", 20);
        var ticket = await CreateTicketAsync(customer, category, priority, "Too large", "Attempt oversized file.");
        var largePayload = new string('A', 10 * 1024 * 1024 + 1);

        using var request = NewMultipartRequest(
            HttpMethod.Post,
            $"/api/tickets/{ticket.Id}/attachments",
            AuthRoles.Customer,
            customer.ApplicationUserId!.Value,
            "too-large.txt",
            "text/plain",
            largePayload);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("maximum allowed size", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListAndDownloadAttachments_RespectSameOwnershipScoping_AsUpload()
    {
        var owner = await CreateCustomerAsync("Attach List Owner", "Contoso");
        var otherCustomer = await CreateCustomerAsync("Attach List Other", "Fabrikam");
        var category = await CreateCategoryAsync($"General-{Guid.NewGuid():N}");
        var priority = await CreatePriorityAsync($"Normal-{Guid.NewGuid():N}", 20);
        var ticket = await CreateTicketAsync(owner, category, priority, "List scope ticket", "Owner-only visibility.");

        var uploaded = await UploadAttachmentAsync(
            ticket.Id,
            AuthRoles.Customer,
            owner.ApplicationUserId!.Value,
            "report.pdf",
            "application/pdf",
            "pdf-bytes");

        using var ownedListRequest = NewRoleRequest(HttpMethod.Get, $"/api/tickets/{ticket.Id}/attachments", AuthRoles.Customer, owner.ApplicationUserId!.Value);
        var ownedListResponse = await _client.SendAsync(ownedListRequest);
        Assert.Equal(HttpStatusCode.OK, ownedListResponse.StatusCode);
        var ownedAttachments = await ownedListResponse.Content.ReadFromJsonAsync<List<TicketAttachmentResponse>>();
        Assert.NotNull(ownedAttachments);
        Assert.Single(ownedAttachments!);

        using var ownedDownloadRequest = NewRoleRequest(
            HttpMethod.Get,
            $"/api/tickets/{ticket.Id}/attachments/{uploaded.Id}/content",
            AuthRoles.Customer,
            owner.ApplicationUserId!.Value);
        var ownedDownloadResponse = await _client.SendAsync(ownedDownloadRequest);
        Assert.Equal(HttpStatusCode.OK, ownedDownloadResponse.StatusCode);
        Assert.Equal("application/pdf", ownedDownloadResponse.Content.Headers.ContentType?.MediaType);
        var downloadedBody = await ownedDownloadResponse.Content.ReadAsStringAsync();
        Assert.Equal("pdf-bytes", downloadedBody);

        using var nonOwnedListRequest = NewRoleRequest(HttpMethod.Get, $"/api/tickets/{ticket.Id}/attachments", AuthRoles.Customer, otherCustomer.ApplicationUserId!.Value);
        var nonOwnedListResponse = await _client.SendAsync(nonOwnedListRequest);
        Assert.Equal(HttpStatusCode.NotFound, nonOwnedListResponse.StatusCode);

        using var nonOwnedDownloadRequest = NewRoleRequest(
            HttpMethod.Get,
            $"/api/tickets/{ticket.Id}/attachments/{uploaded.Id}/content",
            AuthRoles.Customer,
            otherCustomer.ApplicationUserId!.Value);
        var nonOwnedDownloadResponse = await _client.SendAsync(nonOwnedDownloadRequest);
        Assert.Equal(HttpStatusCode.NotFound, nonOwnedDownloadResponse.StatusCode);
    }

    [Fact]
    public async Task UploadAttachment_CleansUpStoredFile_WhenPersistenceFails()
    {
        var customer = await CreateCustomerAsync("Attach Cleanup Customer", "Contoso");
        var category = await CreateCategoryAsync($"General-{Guid.NewGuid():N}");
        var priority = await CreatePriorityAsync($"Normal-{Guid.NewGuid():N}", 20);
        var ticket = await CreateTicketAsync(customer, category, priority, "Cleanup ticket", "Trigger a simulated failure.");

        var recordingStorage = new RecordingAttachmentStorage();

        await using var customizedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAttachmentStorage>();
                services.AddSingleton<IAttachmentStorage>(recordingStorage);

                services.RemoveAll<Microsoft.EntityFrameworkCore.DbContextOptions<CustomerManagementDbContext>>();
                services.AddDbContext<CustomerManagementDbContext>(options =>
                    options.UseInMemoryDatabase(_factory.DatabaseName).AddInterceptors(new ThrowOnTicketAttachmentSaveInterceptor()));
            });
        });

        using var client = customizedFactory.CreateClient();

        using var request = NewMultipartRequest(
            HttpMethod.Post,
            $"/api/tickets/{ticket.Id}/attachments",
            AuthRoles.Customer,
            customer.ApplicationUserId!.Value,
            "force-failure.bin",
            "application/json",
            "payload that triggers a simulated save failure");

        HttpResponseMessage? response = null;
        var threw = false;
        try
        {
            response = await client.SendAsync(request);
        }
        catch
        {
            threw = true;
        }

        Assert.True(threw || response?.StatusCode == HttpStatusCode.InternalServerError);
        Assert.True(recordingStorage.SaveWasCalled);
        Assert.True(recordingStorage.DeleteWasCalled);
    }

    private async Task<CustomerProfileResponse> CreateCustomerAsync(string name, string company)
    {
        var email = $"ticket-attach-{Guid.NewGuid():N}@crm.local";
        var registerRequest = new RegisterRequest(
            email,
            "Agent!23456",
            "Agent!23456",
            name,
            "customer",
            name,
            company,
            [new RegisterContactDetailRequest(1, email, "work", true)]);

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        var tokenPayload = await registerResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokenPayload);
        var userId = Guid.Parse(tokenPayload!.UserId);

        using var listRequest = NewRoleRequest(HttpMethod.Get, "/api/customers?page=1&pageSize=200", AuthRoles.Agent, AgentOneUserId);
        var listResponse = await _client.SendAsync(listRequest);
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<CustomerListResponse>();
        Assert.NotNull(listPayload);

        var customer = listPayload!.Items.FirstOrDefault(item => item.ApplicationUserId == userId);
        Assert.NotNull(customer);

        using var profileRequest = NewRoleRequest(HttpMethod.Get, $"/api/customers/{customer!.Id}", AuthRoles.Agent, AgentOneUserId);
        var profileResponse = await _client.SendAsync(profileRequest);
        profileResponse.EnsureSuccessStatusCode();

        var profile = await profileResponse.Content.ReadFromJsonAsync<CustomerProfileResponse>();
        Assert.NotNull(profile);
        return profile!;
    }

    private async Task<TicketCategoryResponse> CreateCategoryAsync(string name)
    {
        var request = new CreateTicketCategoryRequest(name, null);
        using var httpRequest = NewRoleRequest(HttpMethod.Post, "/api/tickets/categories", AuthRoles.Admin, AdminUserId, request);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TicketCategoryResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task<TicketPriorityResponse> CreatePriorityAsync(string name, int sortOrder)
    {
        var request = new CreateTicketPriorityRequest(name, sortOrder);
        using var httpRequest = NewRoleRequest(HttpMethod.Post, "/api/tickets/priorities", AuthRoles.Admin, AdminUserId, request);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TicketPriorityResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task<TicketResponse> CreateTicketAsync(
        CustomerProfileResponse customer,
        TicketCategoryResponse category,
        TicketPriorityResponse priority,
        string subject,
        string description)
    {
        var createRequest = new CreateTicketRequest(customer.ApplicationUserId, category.Id, priority.Id, subject, description);
        using var request = NewRoleRequest(HttpMethod.Post, "/api/tickets", AuthRoles.Agent, AgentOneUserId, createRequest);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TicketResponse>();
        Assert.NotNull(payload);

        // EF Core InMemory provider never auto-generates RowVersion; set a deterministic non-empty value so
        // subsequent RowVersion-gated calls (e.g. assign) pass both validation and the concurrency check.
        var sentinelRowVersion = new byte[] { 1 };
        await SetTicketRowVersionAsync(payload!.Id, sentinelRowVersion);
        return payload with { RowVersion = sentinelRowVersion };
    }

    private async Task SetTicketRowVersionAsync(Guid ticketId, byte[] rowVersion)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        var ticket = await dbContext.Tickets.FirstAsync(row => row.Id == ticketId);
        ticket.RowVersion = rowVersion;
        await dbContext.SaveChangesAsync();
    }

    private async Task AssignTicketAsync(TicketResponse ticket, Guid assigneeUserId)
    {
        var assignRequest = new AssignTicketRequest(assigneeUserId, ticket.RowVersion);
        using var request = NewRoleRequest(HttpMethod.Put, $"/api/tickets/{ticket.Id}/assign", AuthRoles.Admin, AdminUserId, assignRequest);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private async Task<TicketAttachmentResponse> UploadAttachmentAsync(
        Guid ticketId,
        string role,
        Guid userId,
        string fileName,
        string contentType,
        string content)
    {
        using var request = NewMultipartRequest(HttpMethod.Post, $"/api/tickets/{ticketId}/attachments", role, userId, fileName, contentType, content);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TicketAttachmentResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private static HttpRequestMessage NewMultipartRequest(
        HttpMethod method,
        string uri,
        string role,
        Guid userId,
        string fileName,
        string contentType,
        string content)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(TestAuthDefaults.RoleHeader, role);
        request.Headers.Add(TestAuthDefaults.UserIdHeader, userId.ToString());

        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        request.Content = multipart;

        return request;
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

    private sealed class RecordingAttachmentStorage : IAttachmentStorage
    {
        public bool SaveWasCalled { get; private set; }

        public bool DeleteWasCalled { get; private set; }

        public async Task<StoredAttachment> SaveAsync(
            string originalFileName,
            string contentType,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            SaveWasCalled = true;
            using var memoryStream = new MemoryStream();
            await content.CopyToAsync(memoryStream, cancellationToken);
            return new StoredAttachment(Guid.NewGuid().ToString("N"), memoryStream.Length, originalFileName, contentType);
        }

        public Task<AttachmentContent> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not needed for the cleanup test.");
        }

        public Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            DeleteWasCalled = true;
            return Task.FromResult(true);
        }
    }

    private sealed class ThrowOnTicketAttachmentSaveInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            ThrowIfSentinelAttachmentPresent(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfSentinelAttachmentPresent(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void ThrowIfSentinelAttachmentPresent(Microsoft.EntityFrameworkCore.DbContext? context)
        {
            if (context is not CustomerManagementDbContext dbContext)
            {
                return;
            }

            var hasSentinel = dbContext.ChangeTracker.Entries<TicketAttachment>()
                .Any(entry => entry.State == EntityState.Added && entry.Entity.OriginalFileName == "force-failure.bin");

            if (hasSentinel)
            {
                throw new DbUpdateException("Simulated failure for ticket attachment cleanup test.");
            }
        }
    }
}
