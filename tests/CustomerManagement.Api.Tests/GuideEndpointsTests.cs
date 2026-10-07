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

public sealed class GuideEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;
    private readonly CustomerManagementApiFactory _factory;

    public GuideEndpointsTests(CustomerManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateGuide_AsAdmin_ReturnsCreated_WithStepsInOrder()
    {
        var request = new CreateGuideRequest(
            $"Order-didnt-arrive-{Guid.NewGuid():N}",
            ["Check the tracking page.", "Contact the carrier.", "Request a replacement."]);

        using var httpRequest = NewRequest(HttpMethod.Post, "/api/guides", AuthRoles.Admin, request);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<GuideResponse>();
        Assert.NotNull(payload);
        Assert.Equal(request.Title, payload!.Title);
        Assert.True(payload.IsActive);
        Assert.Equal(3, payload.Steps.Count);
        Assert.Equal([1, 2, 3], payload.Steps.Select(step => step.StepNumber));
        Assert.Equal(request.Steps, payload.Steps.Select(step => step.Instruction));
    }

    [Theory]
    [InlineData(AuthRoles.Agent)]
    [InlineData(AuthRoles.Customer)]
    public async Task CreateGuide_AsNonAdmin_ReturnsForbidden(string role)
    {
        var request = new CreateGuideRequest($"Title-{Guid.NewGuid():N}", ["Step one."]);
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/guides", role, request);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(AuthRoles.Agent)]
    [InlineData(AuthRoles.Customer)]
    public async Task UpdateGuide_AsNonAdmin_ReturnsForbidden(string role)
    {
        var guide = await CreateGuideAsAdminAsync();

        var updateRequest = new UpdateGuideRequest(
            guide.Title,
            guide.Steps.Select(step => step.Instruction).ToList(),
            guide.IsActive,
            guide.RowVersion);
        using var httpRequest = NewRequest(HttpMethod.Put, $"/api/guides/{guide.Id}", role, updateRequest);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateGuide_WithEmptyStepsList_ReturnsValidationError()
    {
        var request = new CreateGuideRequest($"Title-{Guid.NewGuid():N}", []);
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/guides", AuthRoles.Admin, request);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("steps", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListGuides_AsCustomer_OnlyReturnsActiveEntries_EvenWhenActiveOnlyFalseRequested()
    {
        var active = await CreateGuideAsAdminAsync(title: $"Active-{Guid.NewGuid():N}");
        var retired = await CreateGuideAsAdminAsync(title: $"Retired-{Guid.NewGuid():N}");
        retired = await RetireGuideAsAdminAsync(retired);

        using var httpRequest = NewRequest(HttpMethod.Get, "/api/guides?activeOnly=false", AuthRoles.Customer);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<List<GuideResponse>>();
        Assert.NotNull(entries);
        Assert.Contains(entries!, entry => entry.Id == active.Id);
        Assert.DoesNotContain(entries!, entry => entry.Id == retired.Id);
    }

    [Theory]
    [InlineData(AuthRoles.Agent)]
    [InlineData(AuthRoles.Admin)]
    public async Task ListGuides_AsAgentOrAdmin_WithoutActiveOnly_ReturnsActiveAndRetiredEntries(string role)
    {
        var active = await CreateGuideAsAdminAsync(title: $"Active-{Guid.NewGuid():N}");
        var retired = await CreateGuideAsAdminAsync(title: $"Retired-{Guid.NewGuid():N}");
        retired = await RetireGuideAsAdminAsync(retired);

        using var httpRequest = NewRequest(HttpMethod.Get, "/api/guides", role);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<List<GuideResponse>>();
        Assert.NotNull(entries);
        Assert.Contains(entries!, entry => entry.Id == active.Id);
        Assert.Contains(entries!, entry => entry.Id == retired.Id && !entry.IsActive);
    }

    [Fact]
    public async Task UpdateGuide_ReplacesStepList_DoesNotMergeWithPrevious()
    {
        var guide = await CreateGuideAsAdminAsync(steps: ["Old step one.", "Old step two.", "Old step three."]);

        var newSteps = new[] { "New step one.", "New step two." };
        var updateRequest = new UpdateGuideRequest(guide.Title, newSteps, guide.IsActive, guide.RowVersion);
        using var httpRequest = NewRequest(HttpMethod.Put, $"/api/guides/{guide.Id}", AuthRoles.Admin, updateRequest);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<GuideResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.Steps.Count);
        Assert.Equal(newSteps, payload.Steps.Select(step => step.Instruction));
        Assert.Equal([1, 2], payload.Steps.Select(step => step.StepNumber));
    }

    [Fact]
    public async Task RetireGuide_AsAdmin_RemovesItFromSubsequentCustomerList()
    {
        var guide = await CreateGuideAsAdminAsync(title: $"ToRetire-{Guid.NewGuid():N}");

        using var preCheckRequest = NewRequest(HttpMethod.Get, "/api/guides", AuthRoles.Customer);
        var preCheckResponse = await _client.SendAsync(preCheckRequest);
        var preEntries = await preCheckResponse.Content.ReadFromJsonAsync<List<GuideResponse>>();
        Assert.Contains(preEntries!, row => row.Id == guide.Id);

        var retired = await RetireGuideAsAdminAsync(guide);
        Assert.False(retired.IsActive);

        using var postCheckRequest = NewRequest(HttpMethod.Get, "/api/guides", AuthRoles.Customer);
        var postCheckResponse = await _client.SendAsync(postCheckRequest);
        var postEntries = await postCheckResponse.Content.ReadFromJsonAsync<List<GuideResponse>>();
        Assert.DoesNotContain(postEntries!, row => row.Id == guide.Id);
    }

    [Fact]
    public async Task UpdateGuide_WithStaleRowVersion_ReturnsConflict()
    {
        var guide = await CreateGuideAsAdminAsync();

        var staleUpdate = new UpdateGuideRequest(
            guide.Title,
            guide.Steps.Select(step => step.Instruction).ToList(),
            guide.IsActive,
            [9, 9, 9]);
        using var httpRequest = NewRequest(HttpMethod.Put, $"/api/guides/{guide.Id}", AuthRoles.Admin, staleUpdate);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateGuide_WithUnknownId_ReturnsNotFound()
    {
        var updateRequest = new UpdateGuideRequest("Title", ["Step one."], true, [1]);
        using var httpRequest = NewRequest(HttpMethod.Put, $"/api/guides/{Guid.NewGuid()}", AuthRoles.Admin, updateRequest);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateGuide_WithMissingTitle_ReturnsValidationError()
    {
        var request = new CreateGuideRequest("", ["Step one."]);
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/guides", AuthRoles.Admin, request);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("title", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateGuide_WithStepInstructionTooLong_ReturnsValidationError()
    {
        var request = new CreateGuideRequest($"Title-{Guid.NewGuid():N}", [new string('S', 1001)]);
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/guides", AuthRoles.Admin, request);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("steps", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateGuide_WithTooManySteps_ReturnsValidationError()
    {
        var steps = Enumerable.Range(1, 51).Select(i => $"Step {i}.").ToArray();
        var request = new CreateGuideRequest($"Title-{Guid.NewGuid():N}", steps);
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/guides", AuthRoles.Admin, request);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("steps", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Permissions_GuidesReadIsSeededForAllRoles_GuidesManageIsAdminOnly()
    {
        var agentEmail = $"guides-agent-{Guid.NewGuid():N}@crm.local";
        var agentRegisterResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(agentEmail, "Agent!23456", "Agent!23456", "Guides Agent", "agent"));
        agentRegisterResponse.EnsureSuccessStatusCode();
        var agentPayload = await agentRegisterResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(agentPayload);
        Assert.Contains("guides.read", agentPayload!.Permissions, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("guides.manage", agentPayload.Permissions, StringComparer.OrdinalIgnoreCase);

        var customerEmail = $"guides-customer-{Guid.NewGuid():N}@crm.local";
        var customerRegisterResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                customerEmail,
                "Agent!23456",
                "Agent!23456",
                "Guides Customer",
                "customer",
                "Guides Customer",
                "Contoso",
                [new RegisterContactDetailRequest(1, customerEmail, "work", true)]));
        customerRegisterResponse.EnsureSuccessStatusCode();
        var customerPayload = await customerRegisterResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(customerPayload);
        Assert.Contains("guides.read", customerPayload!.Permissions, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("guides.manage", customerPayload.Permissions, StringComparer.OrdinalIgnoreCase);

        var adminLoginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin@crm.local", "Admin!23456"));
        adminLoginResponse.EnsureSuccessStatusCode();
        var adminPayload = await adminLoginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(adminPayload);
        Assert.Contains("guides.read", adminPayload!.Permissions, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("guides.manage", adminPayload.Permissions, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<GuideResponse> CreateGuideAsAdminAsync(
        string? title = null,
        IReadOnlyList<string>? steps = null)
    {
        var request = new CreateGuideRequest(title ?? $"Title-{Guid.NewGuid():N}", steps ?? ["Step one.", "Step two."]);
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/guides", AuthRoles.Admin, request);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GuideResponse>();
        Assert.NotNull(payload);

        // EF Core InMemory provider never auto-generates RowVersion; set a deterministic non-empty value so
        // subsequent RowVersion-gated calls (e.g. retire) pass both validation and the concurrency check.
        var sentinelRowVersion = new byte[] { 1 };
        await SetGuideRowVersionAsync(payload!.Id, sentinelRowVersion);
        return payload with { RowVersion = sentinelRowVersion };
    }

    private async Task SetGuideRowVersionAsync(Guid guideId, byte[] rowVersion)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        var entry = await dbContext.Guides.FirstAsync(row => row.Id == guideId);
        entry.RowVersion = rowVersion;
        await dbContext.SaveChangesAsync();
    }

    private async Task<GuideResponse> RetireGuideAsAdminAsync(GuideResponse guide)
    {
        var updateRequest = new UpdateGuideRequest(
            guide.Title,
            guide.Steps.Select(step => step.Instruction).ToList(),
            false,
            guide.RowVersion);
        using var httpRequest = NewRequest(HttpMethod.Put, $"/api/guides/{guide.Id}", AuthRoles.Admin, updateRequest);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GuideResponse>();
        Assert.NotNull(payload);

        var sentinelRowVersion = new byte[] { 1 };
        await SetGuideRowVersionAsync(payload!.Id, sentinelRowVersion);
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
