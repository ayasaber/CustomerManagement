using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.Auth;
using CustomerManagement.Api.Contracts.KnowledgeBase;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using CustomerManagement.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerManagement.Api.Tests;

public sealed class HelpArticleEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;
    private readonly CustomerManagementApiFactory _factory;

    public HelpArticleEndpointsTests(CustomerManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateHelpArticle_AsAdmin_ReturnsCreated()
    {
        var request = new CreateHelpArticleRequest(
            $"Getting-started-{Guid.NewGuid():N}",
            "Go to Settings > Account to configure your profile.");

        using var httpRequest = NewRequest(HttpMethod.Post, "/api/help-articles", AuthRoles.Admin, request);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<HelpArticleResponse>();
        Assert.NotNull(payload);
        Assert.Equal(request.Title, payload!.Title);
        Assert.Equal(request.Body, payload.Body);
        Assert.True(payload.IsActive);
    }

    [Theory]
    [InlineData(AuthRoles.Agent)]
    [InlineData(AuthRoles.Customer)]
    public async Task CreateHelpArticle_AsNonAdmin_ReturnsForbidden(string role)
    {
        var request = new CreateHelpArticleRequest($"Title-{Guid.NewGuid():N}", "Body text");
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/help-articles", role, request);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(AuthRoles.Agent)]
    [InlineData(AuthRoles.Customer)]
    public async Task UpdateHelpArticle_AsNonAdmin_ReturnsForbidden(string role)
    {
        var article = await CreateHelpArticleAsAdminAsync();

        var updateRequest = new UpdateHelpArticleRequest(article.Title, article.Body, article.IsActive, article.RowVersion);
        using var httpRequest = NewRequest(HttpMethod.Put, $"/api/help-articles/{article.Id}", role, updateRequest);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListHelpArticles_AsCustomer_OnlyReturnsActiveEntries_EvenWhenActiveOnlyFalseRequested()
    {
        var active = await CreateHelpArticleAsAdminAsync(title: $"Active-{Guid.NewGuid():N}");
        var retired = await CreateHelpArticleAsAdminAsync(title: $"Retired-{Guid.NewGuid():N}");
        retired = await RetireHelpArticleAsAdminAsync(retired);

        using var httpRequest = NewRequest(HttpMethod.Get, "/api/help-articles?activeOnly=false", AuthRoles.Customer);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<List<HelpArticleResponse>>();
        Assert.NotNull(entries);
        Assert.Contains(entries!, entry => entry.Id == active.Id);
        Assert.DoesNotContain(entries!, entry => entry.Id == retired.Id);
    }

    [Theory]
    [InlineData(AuthRoles.Agent)]
    [InlineData(AuthRoles.Admin)]
    public async Task ListHelpArticles_AsAgentOrAdmin_WithoutActiveOnly_ReturnsActiveAndRetiredEntries(string role)
    {
        var active = await CreateHelpArticleAsAdminAsync(title: $"Active-{Guid.NewGuid():N}");
        var retired = await CreateHelpArticleAsAdminAsync(title: $"Retired-{Guid.NewGuid():N}");
        retired = await RetireHelpArticleAsAdminAsync(retired);

        using var httpRequest = NewRequest(HttpMethod.Get, "/api/help-articles", role);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<List<HelpArticleResponse>>();
        Assert.NotNull(entries);
        Assert.Contains(entries!, entry => entry.Id == active.Id);
        Assert.Contains(entries!, entry => entry.Id == retired.Id && !entry.IsActive);
    }

    [Fact]
    public async Task RetireHelpArticle_AsAdmin_RemovesItFromSubsequentCustomerList()
    {
        var article = await CreateHelpArticleAsAdminAsync(title: $"ToRetire-{Guid.NewGuid():N}");

        using var preCheckRequest = NewRequest(HttpMethod.Get, "/api/help-articles", AuthRoles.Customer);
        var preCheckResponse = await _client.SendAsync(preCheckRequest);
        var preEntries = await preCheckResponse.Content.ReadFromJsonAsync<List<HelpArticleResponse>>();
        Assert.Contains(preEntries!, row => row.Id == article.Id);

        var retired = await RetireHelpArticleAsAdminAsync(article);
        Assert.False(retired.IsActive);

        using var postCheckRequest = NewRequest(HttpMethod.Get, "/api/help-articles", AuthRoles.Customer);
        var postCheckResponse = await _client.SendAsync(postCheckRequest);
        var postEntries = await postCheckResponse.Content.ReadFromJsonAsync<List<HelpArticleResponse>>();
        Assert.DoesNotContain(postEntries!, row => row.Id == article.Id);
    }

    [Fact]
    public async Task UpdateHelpArticle_WithStaleRowVersion_ReturnsConflict()
    {
        var article = await CreateHelpArticleAsAdminAsync();

        var staleUpdate = new UpdateHelpArticleRequest(article.Title, article.Body, article.IsActive, [9, 9, 9]);
        using var httpRequest = NewRequest(HttpMethod.Put, $"/api/help-articles/{article.Id}", AuthRoles.Admin, staleUpdate);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateHelpArticle_WithUnknownId_ReturnsNotFound()
    {
        var updateRequest = new UpdateHelpArticleRequest("Title", "Body", true, [1]);
        using var httpRequest = NewRequest(HttpMethod.Put, $"/api/help-articles/{Guid.NewGuid()}", AuthRoles.Admin, updateRequest);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("", "Valid body", "title")]
    [InlineData("Valid title", "", "body")]
    public async Task CreateHelpArticle_WithMissingRequiredField_ReturnsValidationError(
        string title, string body, string expectedField)
    {
        var request = new CreateHelpArticleRequest(title, body);
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/help-articles", AuthRoles.Admin, request);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedField, responseBody, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(201, 10, "title")]
    [InlineData(10, 10001, "body")]
    public async Task CreateHelpArticle_WithFieldTooLong_ReturnsValidationError(
        int titleLength, int bodyLength, string expectedField)
    {
        var request = new CreateHelpArticleRequest(
            new string('T', titleLength),
            new string('B', bodyLength));

        using var httpRequest = NewRequest(HttpMethod.Post, "/api/help-articles", AuthRoles.Admin, request);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedField, responseBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Permissions_HelpArticlesReadIsSeededForAllRoles_HelpArticlesManageIsAdminOnly()
    {
        var agentEmail = $"help-articles-agent-{Guid.NewGuid():N}@crm.local";
        var agentRegisterResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(agentEmail, "Agent!23456", "Agent!23456", "HelpArticles Agent", "agent"));
        agentRegisterResponse.EnsureSuccessStatusCode();
        var agentPayload = await agentRegisterResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(agentPayload);
        Assert.Contains("help-articles.read", agentPayload!.Permissions, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("help-articles.manage", agentPayload.Permissions, StringComparer.OrdinalIgnoreCase);

        var customerEmail = $"help-articles-customer-{Guid.NewGuid():N}@crm.local";
        var customerRegisterResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                customerEmail,
                "Agent!23456",
                "Agent!23456",
                "HelpArticles Customer",
                "customer",
                "HelpArticles Customer",
                "Contoso",
                [new RegisterContactDetailRequest(1, customerEmail, "work", true)]));
        customerRegisterResponse.EnsureSuccessStatusCode();
        var customerPayload = await customerRegisterResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(customerPayload);
        Assert.Contains("help-articles.read", customerPayload!.Permissions, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("help-articles.manage", customerPayload.Permissions, StringComparer.OrdinalIgnoreCase);

        var adminLoginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin@crm.local", "Admin!23456"));
        adminLoginResponse.EnsureSuccessStatusCode();
        var adminPayload = await adminLoginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(adminPayload);
        Assert.Contains("help-articles.read", adminPayload!.Permissions, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("help-articles.manage", adminPayload.Permissions, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<HelpArticleResponse> CreateHelpArticleAsAdminAsync(
        string? title = null,
        string body = "Here is the article body.")
    {
        var request = new CreateHelpArticleRequest(title ?? $"Title-{Guid.NewGuid():N}", body);
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/help-articles", AuthRoles.Admin, request);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<HelpArticleResponse>();
        Assert.NotNull(payload);

        // EF Core InMemory provider never auto-generates RowVersion; set a deterministic non-empty value so
        // subsequent RowVersion-gated calls (e.g. retire) pass both validation and the concurrency check.
        var sentinelRowVersion = new byte[] { 1 };
        await SetHelpArticleRowVersionAsync(payload!.Id, sentinelRowVersion);
        return payload with { RowVersion = sentinelRowVersion };
    }

    private async Task SetHelpArticleRowVersionAsync(Guid articleId, byte[] rowVersion)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        var entry = await dbContext.HelpArticles.FirstAsync(row => row.Id == articleId);
        entry.RowVersion = rowVersion;
        await dbContext.SaveChangesAsync();
    }

    private async Task<HelpArticleResponse> RetireHelpArticleAsAdminAsync(HelpArticleResponse article)
    {
        var updateRequest = new UpdateHelpArticleRequest(article.Title, article.Body, false, article.RowVersion);
        using var httpRequest = NewRequest(HttpMethod.Put, $"/api/help-articles/{article.Id}", AuthRoles.Admin, updateRequest);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<HelpArticleResponse>();
        Assert.NotNull(payload);

        var sentinelRowVersion = new byte[] { 1 };
        await SetHelpArticleRowVersionAsync(payload!.Id, sentinelRowVersion);
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
