using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.Auth;
using CustomerManagement.Api.Contracts.Faq;
using CustomerManagement.Api.Contracts.KnowledgeBase;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using CustomerManagement.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerManagement.Api.Tests;

public sealed class KnowledgeBaseSearchEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;
    private readonly CustomerManagementApiFactory _factory;

    public KnowledgeBaseSearchEndpointsTests(CustomerManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Search_MatchingTermAcrossAllThreeContentTypes_ReturnsAllWithCorrectContentTypeAndSnippet()
    {
        var term = $"Zephyr{Guid.NewGuid():N}";

        var faq = await CreateFaqAsync(question: $"What is {term}?", answer: "An answer with no special words.");
        var article = await CreateHelpArticleAsync(title: $"Guide to {term}", body: "Body text.");
        var guide = await CreateGuideAsync(
            title: "Generic guide title",
            steps: ["First step, nothing special.", $"Second step mentions {term} here."]);

        using var httpRequest = NewRequest(HttpMethod.Get, $"/api/knowledge-base/search?q={term}", AuthRoles.Admin);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<KnowledgeBaseSearchResponse>();
        Assert.NotNull(payload);

        var faqResult = Assert.Single(payload!.Results, r => r.Id == faq.Id);
        Assert.Equal(nameof(KnowledgeBaseContentType.Faq), faqResult.ContentType);

        var articleResult = Assert.Single(payload.Results, r => r.Id == article.Id);
        Assert.Equal(nameof(KnowledgeBaseContentType.HelpArticle), articleResult.ContentType);

        var guideResult = Assert.Single(payload.Results, r => r.Id == guide.Id);
        Assert.Equal(nameof(KnowledgeBaseContentType.Guide), guideResult.ContentType);
        Assert.Contains(term, guideResult.Snippet, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Second step", guideResult.Snippet);
    }

    [Theory]
    [InlineData(AuthRoles.Admin)]
    [InlineData(AuthRoles.Agent)]
    [InlineData(AuthRoles.Customer)]
    public async Task Search_RetiredContentOfAnyType_NeverAppearsForAnyRole(string role)
    {
        var term = $"Retirewood{Guid.NewGuid():N}";

        var faq = await CreateFaqAsync(question: $"Retired question about {term}?", answer: "Answer text.");
        await RetireFaqAsync(faq);

        var article = await CreateHelpArticleAsync(title: $"Retired article {term}", body: "Body text.");
        await RetireHelpArticleAsync(article);

        var guide = await CreateGuideAsync(title: $"Retired guide {term}", steps: ["Step one."]);
        await RetireGuideAsync(guide);

        using var httpRequest = NewRequest(HttpMethod.Get, $"/api/knowledge-base/search?q={term}", role);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<KnowledgeBaseSearchResponse>();
        Assert.NotNull(payload);
        Assert.DoesNotContain(payload!.Results, r => r.Id == faq.Id);
        Assert.DoesNotContain(payload.Results, r => r.Id == article.Id);
        Assert.DoesNotContain(payload.Results, r => r.Id == guide.Id);
    }

    [Fact]
    public async Task Search_TermMatchingOnlyFaqAnswer_StillReturnsFaq_ScoredLowerThanTitleMatch()
    {
        var term = $"Bodyword{Guid.NewGuid():N}";

        var bodyOnlyMatch = await CreateFaqAsync(question: "Unrelated question", answer: $"The answer mentions {term} here.");
        var titleMatch = await CreateFaqAsync(question: $"A question about {term}", answer: "Unrelated answer.");

        using var httpRequest = NewRequest(HttpMethod.Get, $"/api/knowledge-base/search?q={term}", AuthRoles.Admin);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<KnowledgeBaseSearchResponse>();
        Assert.NotNull(payload);

        var ids = payload!.Results.Select(r => r.Id).ToList();
        Assert.Contains(bodyOnlyMatch.Id, ids);
        Assert.Contains(titleMatch.Id, ids);
        Assert.True(ids.IndexOf(titleMatch.Id) < ids.IndexOf(bodyOnlyMatch.Id));
    }

    [Fact]
    public async Task Search_WithEmptyQuery_ReturnsEmptyResults()
    {
        using var httpRequest = NewRequest(HttpMethod.Get, "/api/knowledge-base/search?q=", AuthRoles.Admin);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<KnowledgeBaseSearchResponse>();
        Assert.NotNull(payload);
        Assert.Empty(payload!.Results);
    }

    [Fact]
    public async Task Search_WithNoMatches_ReturnsEmptyResults()
    {
        var term = $"NoMatchesAnywhere{Guid.NewGuid():N}";
        using var httpRequest = NewRequest(HttpMethod.Get, $"/api/knowledge-base/search?q={term}", AuthRoles.Admin);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<KnowledgeBaseSearchResponse>();
        Assert.NotNull(payload);
        Assert.Empty(payload!.Results);
    }

    [Theory]
    [InlineData(AuthRoles.Admin)]
    [InlineData(AuthRoles.Agent)]
    [InlineData(AuthRoles.Customer)]
    public async Task Search_AllThreeRoles_Succeed(string role)
    {
        using var httpRequest = NewRequest(HttpMethod.Get, "/api/knowledge-base/search?q=anything", role);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Permissions_KnowledgeBaseSearchIsSeededForAllRoles()
    {
        var agentEmail = $"kb-search-agent-{Guid.NewGuid():N}@crm.local";
        var agentRegisterResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(agentEmail, "Agent!23456", "Agent!23456", "KnowledgeBase Agent", "agent"));
        agentRegisterResponse.EnsureSuccessStatusCode();
        var agentPayload = await agentRegisterResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(agentPayload);
        Assert.Contains("knowledge-base.search", agentPayload!.Permissions, StringComparer.OrdinalIgnoreCase);

        var customerEmail = $"kb-search-customer-{Guid.NewGuid():N}@crm.local";
        var customerRegisterResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                customerEmail,
                "Agent!23456",
                "Agent!23456",
                "KnowledgeBase Customer",
                "customer",
                "KnowledgeBase Customer",
                "Contoso",
                [new RegisterContactDetailRequest(1, customerEmail, "work", true)]));
        customerRegisterResponse.EnsureSuccessStatusCode();
        var customerPayload = await customerRegisterResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(customerPayload);
        Assert.Contains("knowledge-base.search", customerPayload!.Permissions, StringComparer.OrdinalIgnoreCase);

        var adminLoginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin@crm.local", "Admin!23456"));
        adminLoginResponse.EnsureSuccessStatusCode();
        var adminPayload = await adminLoginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(adminPayload);
        Assert.Contains("knowledge-base.search", adminPayload!.Permissions, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<FaqEntryResponse> CreateFaqAsync(string question, string answer)
    {
        var request = new CreateFaqEntryRequest($"Topic-{Guid.NewGuid():N}", question, answer, 10);
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/faq", AuthRoles.Admin, request);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<FaqEntryResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task RetireFaqAsync(FaqEntryResponse entry)
    {
        var sentinelRowVersion = new byte[] { 1 };
        await SetRowVersionAsync<CustomerManagement.Api.Domain.Faq.FaqEntry>(dbSet => dbSet, entry.Id, sentinelRowVersion);

        var updateRequest = new UpdateFaqEntryRequest(entry.Topic, entry.Question, entry.Answer, entry.SortOrder, false, sentinelRowVersion);
        using var httpRequest = NewRequest(HttpMethod.Put, $"/api/faq/{entry.Id}", AuthRoles.Admin, updateRequest);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HelpArticleResponse> CreateHelpArticleAsync(string title, string body)
    {
        var request = new CreateHelpArticleRequest(title, body);
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/help-articles", AuthRoles.Admin, request);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<HelpArticleResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task RetireHelpArticleAsync(HelpArticleResponse entry)
    {
        var sentinelRowVersion = new byte[] { 1 };
        await SetRowVersionAsync<CustomerManagement.Api.Domain.KnowledgeBase.HelpArticle>(dbSet => dbSet, entry.Id, sentinelRowVersion);

        var updateRequest = new UpdateHelpArticleRequest(entry.Title, entry.Body, false, sentinelRowVersion);
        using var httpRequest = NewRequest(HttpMethod.Put, $"/api/help-articles/{entry.Id}", AuthRoles.Admin, updateRequest);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
    }

    private async Task<GuideResponse> CreateGuideAsync(string title, IReadOnlyList<string> steps)
    {
        var request = new CreateGuideRequest(title, steps);
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/guides", AuthRoles.Admin, request);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GuideResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task RetireGuideAsync(GuideResponse entry)
    {
        var sentinelRowVersion = new byte[] { 1 };
        await SetRowVersionAsync<CustomerManagement.Api.Domain.KnowledgeBase.Guide>(dbSet => dbSet, entry.Id, sentinelRowVersion);

        var updateRequest = new UpdateGuideRequest(
            entry.Title,
            entry.Steps.Select(step => step.Instruction).ToList(),
            false,
            sentinelRowVersion);
        using var httpRequest = NewRequest(HttpMethod.Put, $"/api/guides/{entry.Id}", AuthRoles.Admin, updateRequest);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
    }

    // EF Core InMemory never auto-generates RowVersion; set a deterministic non-empty value directly so the
    // retire update's RowVersion-gated concurrency check passes, same pattern used across the other KB test files.
    private async Task SetRowVersionAsync<TEntity>(
        Func<DbSet<TEntity>, DbSet<TEntity>> selectSet,
        Guid id,
        byte[] rowVersion)
        where TEntity : class
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();
        var set = selectSet(dbContext.Set<TEntity>());

        var entry = await set.FindAsync([id]);
        Assert.NotNull(entry);

        dbContext.Entry(entry!).Property("RowVersion").CurrentValue = rowVersion;
        await dbContext.SaveChangesAsync();
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
