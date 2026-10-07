using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.Auth;
using CustomerManagement.Api.Contracts.Feedback;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using CustomerManagement.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerManagement.Api.Tests;

public sealed class FeedbackEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;
    private readonly CustomerManagementApiFactory _factory;

    public FeedbackEndpointsTests(CustomerManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SubmitFeedback_WithRatingAndComment_ReturnsCreated_WithResolvedCustomerName()
    {
        var (userId, displayName) = await RegisterCustomerAsync("Feedback Customer One");

        var request = new CreateFeedbackRequest(5, "Excellent support, thank you!");
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/feedback", AuthRoles.Customer, userId, request);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<FeedbackResponse>();
        Assert.NotNull(payload);
        Assert.Equal(5, payload!.Rating);
        Assert.Equal("Excellent support, thank you!", payload.Comment);
        Assert.Equal(displayName, payload.CustomerName);
    }

    [Fact]
    public async Task SubmitFeedback_WithRatingOnly_ReturnsCreated_WithNullComment()
    {
        var (userId, _) = await RegisterCustomerAsync("Feedback Customer Two");

        var request = new CreateFeedbackRequest(4, null);
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/feedback", AuthRoles.Customer, userId, request);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<FeedbackResponse>();
        Assert.NotNull(payload);
        Assert.Equal(4, payload!.Rating);
        Assert.Null(payload.Comment);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task SubmitFeedback_WithRatingOutOfRange_ReturnsBadRequest(int rating)
    {
        var (userId, _) = await RegisterCustomerAsync("Feedback Rating Customer");

        var request = new CreateFeedbackRequest(rating, "Some comment");
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/feedback", AuthRoles.Customer, userId, request);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("rating", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitFeedback_WithCommentTooLong_ReturnsBadRequest()
    {
        var (userId, _) = await RegisterCustomerAsync("Feedback Comment Customer");

        var request = new CreateFeedbackRequest(3, new string('A', 2001));
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/feedback", AuthRoles.Customer, userId, request);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("comment", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AuthRoles.Agent)]
    [InlineData(AuthRoles.Admin)]
    public async Task SubmitFeedback_AsAgentOrAdmin_ReturnsForbidden(string role)
    {
        var request = new CreateFeedbackRequest(5, "Attempted submission");
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/feedback", role, body: request);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListFeedback_AsCustomer_ReturnsForbidden()
    {
        using var httpRequest = NewRequest(HttpMethod.Get, "/api/feedback", AuthRoles.Customer);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(AuthRoles.Admin)]
    [InlineData(AuthRoles.Agent)]
    public async Task ListFeedback_AsAdminOrAgent_ReturnsPaginatedResults_NewestFirst(string role)
    {
        var (userId, _) = await RegisterCustomerAsync($"Feedback List Customer {Guid.NewGuid():N}");

        var oldest = await SubmitFeedbackAsync(userId, 3, "Oldest");
        var middle = await SubmitFeedbackAsync(userId, 4, "Middle");
        var newest = await SubmitFeedbackAsync(userId, 5, "Newest");

        // Use timestamps far in the future so this trio is unambiguously ordered relative to each other,
        // regardless of how many other entries other tests/iterations have added to the shared database.
        var baseline = DateTime.UtcNow.AddDays(1);
        await SetFeedbackCreatedAtAsync(oldest.Id, baseline);
        await SetFeedbackCreatedAtAsync(middle.Id, baseline.AddMinutes(10));
        await SetFeedbackCreatedAtAsync(newest.Id, baseline.AddMinutes(20));

        using var httpRequest = NewRequest(HttpMethod.Get, "/api/feedback?page=1&pageSize=100", role);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<FeedbackListResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.Page);
        Assert.Equal(100, payload.PageSize);
        Assert.True(payload.TotalCount >= 3);

        var ids = payload.Items.Select(item => item.Id).ToList();
        var newestIndex = ids.IndexOf(newest.Id);
        var middleIndex = ids.IndexOf(middle.Id);
        var oldestIndex = ids.IndexOf(oldest.Id);

        Assert.True(newestIndex >= 0 && middleIndex >= 0 && oldestIndex >= 0);
        Assert.True(newestIndex < middleIndex, "Newest entry should be listed before the middle entry.");
        Assert.True(middleIndex < oldestIndex, "Middle entry should be listed before the oldest entry.");
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task ListFeedback_WithInvalidPaging_ReturnsBadRequest(int page, int pageSize)
    {
        using var httpRequest = NewRequest(HttpMethod.Get, $"/api/feedback?page={page}&pageSize={pageSize}", AuthRoles.Admin);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Permissions_FeedbackSubmitIsCustomerOnly_FeedbackReadIsAdminAndAgentOnly()
    {
        var agentEmail = $"feedback-agent-{Guid.NewGuid():N}@crm.local";
        var agentResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(agentEmail, "Agent!23456", "Agent!23456", "Feedback Agent", "agent"));
        agentResponse.EnsureSuccessStatusCode();
        var agentPayload = await agentResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(agentPayload);
        Assert.Contains("feedback.read", agentPayload!.Permissions, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("feedback.submit", agentPayload.Permissions, StringComparer.OrdinalIgnoreCase);

        var customerEmail = $"feedback-customer-{Guid.NewGuid():N}@crm.local";
        var customerResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                customerEmail,
                "Agent!23456",
                "Agent!23456",
                "Feedback Customer Perm",
                "customer",
                "Feedback Customer Perm",
                "Contoso",
                [new RegisterContactDetailRequest(1, customerEmail, "work", true)]));
        customerResponse.EnsureSuccessStatusCode();
        var customerPayload = await customerResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(customerPayload);
        Assert.Contains("feedback.submit", customerPayload!.Permissions, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("feedback.read", customerPayload.Permissions, StringComparer.OrdinalIgnoreCase);

        var adminLoginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin@crm.local", "Admin!23456"));
        adminLoginResponse.EnsureSuccessStatusCode();
        var adminPayload = await adminLoginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(adminPayload);
        Assert.Contains("feedback.read", adminPayload!.Permissions, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("feedback.submit", adminPayload.Permissions, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<(Guid UserId, string DisplayName)> RegisterCustomerAsync(string displayName)
    {
        var email = $"feedback-{Guid.NewGuid():N}@crm.local";
        var request = new RegisterRequest(
            email,
            "Agent!23456",
            "Agent!23456",
            displayName,
            "customer",
            displayName,
            "Contoso",
            [new RegisterContactDetailRequest(1, email, "work", true)]);

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(payload);
        return (Guid.Parse(payload!.UserId), displayName);
    }

    private async Task<FeedbackResponse> SubmitFeedbackAsync(Guid userId, int rating, string? comment)
    {
        var request = new CreateFeedbackRequest(rating, comment);
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/feedback", AuthRoles.Customer, userId, request);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<FeedbackResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task SetFeedbackCreatedAtAsync(Guid feedbackId, DateTime createdAtUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        var feedback = await dbContext.FeedbackEntries.FirstAsync(row => row.Id == feedbackId);
        feedback.CreatedAtUtc = createdAtUtc;
        await dbContext.SaveChangesAsync();
    }

    private static HttpRequestMessage NewRequest(HttpMethod method, string uri, string role, Guid? userId = null, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(TestAuthDefaults.RoleHeader, role);

        if (userId.HasValue)
        {
            request.Headers.Add(TestAuthDefaults.UserIdHeader, userId.Value.ToString());
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }
}
