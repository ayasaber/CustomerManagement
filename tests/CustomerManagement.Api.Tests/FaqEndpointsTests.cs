using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.Auth;
using CustomerManagement.Api.Contracts.Faq;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using CustomerManagement.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerManagement.Api.Tests;

public sealed class FaqEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;
    private readonly CustomerManagementApiFactory _factory;

    public FaqEndpointsTests(CustomerManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateFaqEntry_AsAdmin_ReturnsCreated()
    {
        var request = new CreateFaqEntryRequest(
            $"Billing-{Guid.NewGuid():N}",
            "How do I update my payment method?",
            "Go to Billing > Payment Methods and add a new card.",
            10);

        using var httpRequest = NewRequest(HttpMethod.Post, "/api/faq", AuthRoles.Admin, request);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<FaqEntryResponse>();
        Assert.NotNull(payload);
        Assert.Equal(request.Topic, payload!.Topic);
        Assert.Equal(request.Question, payload.Question);
        Assert.Equal(request.Answer, payload.Answer);
        Assert.True(payload.IsActive);
    }

    [Theory]
    [InlineData(AuthRoles.Agent)]
    [InlineData(AuthRoles.Customer)]
    public async Task CreateFaqEntry_AsNonAdmin_ReturnsForbidden(string role)
    {
        var request = new CreateFaqEntryRequest($"Topic-{Guid.NewGuid():N}", "Question text", "Answer text", 10);
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/faq", role, request);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(AuthRoles.Agent)]
    [InlineData(AuthRoles.Customer)]
    public async Task UpdateFaqEntry_AsNonAdmin_ReturnsForbidden(string role)
    {
        var entry = await CreateFaqEntryAsAdminAsync();

        var updateRequest = new UpdateFaqEntryRequest(entry.Topic, entry.Question, entry.Answer, entry.SortOrder, entry.IsActive, entry.RowVersion);
        using var httpRequest = NewRequest(HttpMethod.Put, $"/api/faq/{entry.Id}", role, updateRequest);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListFaqEntries_AsCustomer_OnlyReturnsActiveEntries_EvenWhenActiveOnlyFalseRequested()
    {
        var active = await CreateFaqEntryAsAdminAsync(topic: $"Active-{Guid.NewGuid():N}");
        var retired = await CreateFaqEntryAsAdminAsync(topic: $"Retired-{Guid.NewGuid():N}");
        retired = await RetireFaqEntryAsAdminAsync(retired);

        using var httpRequest = NewRequest(HttpMethod.Get, "/api/faq?activeOnly=false", AuthRoles.Customer);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<List<FaqEntryResponse>>();
        Assert.NotNull(entries);
        Assert.Contains(entries!, entry => entry.Id == active.Id);
        Assert.DoesNotContain(entries!, entry => entry.Id == retired.Id);
    }

    [Theory]
    [InlineData(AuthRoles.Agent)]
    [InlineData(AuthRoles.Admin)]
    public async Task ListFaqEntries_AsAgentOrAdmin_WithoutActiveOnly_ReturnsActiveAndRetiredEntries(string role)
    {
        var active = await CreateFaqEntryAsAdminAsync(topic: $"Active-{Guid.NewGuid():N}");
        var retired = await CreateFaqEntryAsAdminAsync(topic: $"Retired-{Guid.NewGuid():N}");
        retired = await RetireFaqEntryAsAdminAsync(retired);

        using var httpRequest = NewRequest(HttpMethod.Get, "/api/faq", role);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<List<FaqEntryResponse>>();
        Assert.NotNull(entries);
        Assert.Contains(entries!, entry => entry.Id == active.Id);
        Assert.Contains(entries!, entry => entry.Id == retired.Id && !entry.IsActive);
    }

    [Fact]
    public async Task RetireFaqEntry_AsAdmin_RemovesItFromSubsequentCustomerList()
    {
        var entry = await CreateFaqEntryAsAdminAsync(topic: $"ToRetire-{Guid.NewGuid():N}");

        using var preCheckRequest = NewRequest(HttpMethod.Get, "/api/faq", AuthRoles.Customer);
        var preCheckResponse = await _client.SendAsync(preCheckRequest);
        var preEntries = await preCheckResponse.Content.ReadFromJsonAsync<List<FaqEntryResponse>>();
        Assert.Contains(preEntries!, row => row.Id == entry.Id);

        var retired = await RetireFaqEntryAsAdminAsync(entry);
        Assert.False(retired.IsActive);

        using var postCheckRequest = NewRequest(HttpMethod.Get, "/api/faq", AuthRoles.Customer);
        var postCheckResponse = await _client.SendAsync(postCheckRequest);
        var postEntries = await postCheckResponse.Content.ReadFromJsonAsync<List<FaqEntryResponse>>();
        Assert.DoesNotContain(postEntries!, row => row.Id == entry.Id);
    }

    [Fact]
    public async Task UpdateFaqEntry_WithStaleRowVersion_ReturnsConflict()
    {
        var entry = await CreateFaqEntryAsAdminAsync();

        var staleUpdate = new UpdateFaqEntryRequest(entry.Topic, entry.Question, entry.Answer, entry.SortOrder, entry.IsActive, [9, 9, 9]);
        using var httpRequest = NewRequest(HttpMethod.Put, $"/api/faq/{entry.Id}", AuthRoles.Admin, staleUpdate);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateFaqEntry_WithUnknownId_ReturnsNotFound()
    {
        var updateRequest = new UpdateFaqEntryRequest("Topic", "Question", "Answer", 10, true, [1]);
        using var httpRequest = NewRequest(HttpMethod.Put, $"/api/faq/{Guid.NewGuid()}", AuthRoles.Admin, updateRequest);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("", "Valid question", "Valid answer", "topic")]
    [InlineData("Valid topic", "", "Valid answer", "question")]
    [InlineData("Valid topic", "Valid question", "", "answer")]
    public async Task CreateFaqEntry_WithMissingRequiredField_ReturnsValidationError(
        string topic, string question, string answer, string expectedField)
    {
        var request = new CreateFaqEntryRequest(topic, question, answer, 10);
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/faq", AuthRoles.Admin, request);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedField, body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(151, 10, 10, "topic")]
    [InlineData(10, 501, 10, "question")]
    [InlineData(10, 10, 4001, "answer")]
    public async Task CreateFaqEntry_WithFieldTooLong_ReturnsValidationError(
        int topicLength, int questionLength, int answerLength, string expectedField)
    {
        var request = new CreateFaqEntryRequest(
            new string('T', topicLength),
            new string('Q', questionLength),
            new string('A', answerLength),
            10);

        using var httpRequest = NewRequest(HttpMethod.Post, "/api/faq", AuthRoles.Admin, request);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedField, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Permissions_FaqReadIsSeededForAllRoles_FaqManageIsAdminOnly()
    {
        var agentEmail = $"faq-agent-{Guid.NewGuid():N}@crm.local";
        var agentRegisterResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(agentEmail, "Agent!23456", "Agent!23456", "Faq Agent", "agent"));
        agentRegisterResponse.EnsureSuccessStatusCode();
        var agentPayload = await agentRegisterResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(agentPayload);
        Assert.Contains("faq.read", agentPayload!.Permissions, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("faq.manage", agentPayload.Permissions, StringComparer.OrdinalIgnoreCase);

        var customerEmail = $"faq-customer-{Guid.NewGuid():N}@crm.local";
        var customerRegisterResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                customerEmail,
                "Agent!23456",
                "Agent!23456",
                "Faq Customer",
                "customer",
                "Faq Customer",
                "Contoso",
                [new RegisterContactDetailRequest(1, customerEmail, "work", true)]));
        customerRegisterResponse.EnsureSuccessStatusCode();
        var customerPayload = await customerRegisterResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(customerPayload);
        Assert.Contains("faq.read", customerPayload!.Permissions, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("faq.manage", customerPayload.Permissions, StringComparer.OrdinalIgnoreCase);

        var adminLoginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin@crm.local", "Admin!23456"));
        adminLoginResponse.EnsureSuccessStatusCode();
        var adminPayload = await adminLoginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(adminPayload);
        Assert.Contains("faq.read", adminPayload!.Permissions, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("faq.manage", adminPayload.Permissions, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<FaqEntryResponse> CreateFaqEntryAsAdminAsync(
        string? topic = null,
        string question = "What is covered?",
        string answer = "Here is the answer.",
        int sortOrder = 10)
    {
        var request = new CreateFaqEntryRequest(topic ?? $"Topic-{Guid.NewGuid():N}", question, answer, sortOrder);
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/faq", AuthRoles.Admin, request);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<FaqEntryResponse>();
        Assert.NotNull(payload);

        // EF Core InMemory provider never auto-generates RowVersion; set a deterministic non-empty value so
        // subsequent RowVersion-gated calls (e.g. retire) pass both validation and the concurrency check.
        var sentinelRowVersion = new byte[] { 1 };
        await SetFaqEntryRowVersionAsync(payload!.Id, sentinelRowVersion);
        return payload with { RowVersion = sentinelRowVersion };
    }

    private async Task SetFaqEntryRowVersionAsync(Guid faqId, byte[] rowVersion)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        var entry = await dbContext.FaqEntries.FirstAsync(row => row.Id == faqId);
        entry.RowVersion = rowVersion;
        await dbContext.SaveChangesAsync();
    }

    private async Task<FaqEntryResponse> RetireFaqEntryAsAdminAsync(FaqEntryResponse entry)
    {
        var updateRequest = new UpdateFaqEntryRequest(entry.Topic, entry.Question, entry.Answer, entry.SortOrder, false, entry.RowVersion);
        using var httpRequest = NewRequest(HttpMethod.Put, $"/api/faq/{entry.Id}", AuthRoles.Admin, updateRequest);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<FaqEntryResponse>();
        Assert.NotNull(payload);

        var sentinelRowVersion = new byte[] { 1 };
        await SetFaqEntryRowVersionAsync(payload!.Id, sentinelRowVersion);
        return payload with { RowVersion = sentinelRowVersion };
    }

    private static HttpRequestMessage NewRequest(HttpMethod method, string uri, string role, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(TestAuthDefaults.RoleHeader, role);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }
}
