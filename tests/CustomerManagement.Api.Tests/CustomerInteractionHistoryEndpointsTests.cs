using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.Auth;
using CustomerManagement.Api.Contracts.Customers;
using CustomerManagement.Api.Domain.Customers;
using CustomerManagement.Api.Endpoints.Customers;
using CustomerManagement.Api.Infrastructure.Persistence;
using CustomerManagement.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerManagement.Api.Tests;

public sealed class CustomerInteractionHistoryEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;
    private readonly CustomerManagementApiFactory _factory;

    public CustomerInteractionHistoryEndpointsTests(CustomerManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetInteractionHistory_ReturnsEmpty_WhenNoEventsExist()
    {
        var customer = await CreateCustomerAsync("Hana", "Contoso");

        using var request = NewAgentRequest(HttpMethod.Get, $"/api/customers/{customer.Id}/interaction-history");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CustomerInteractionHistoryResponse>();
        Assert.NotNull(payload);
        Assert.Equal(0, payload!.TotalCount);
        Assert.Empty(payload.Items);
    }

    [Fact]
    public async Task GetInteractionHistory_ReturnsOrderedAndPagedResults()
    {
        var customer = await CreateCustomerAsync("Yara", "Fabrikam");
        await SeedInteractionEventsAsync(customer.Id,
        [
            CreateEvent(customer.Id, InteractionChannel.Email, InteractionDirection.Inbound, "2026-01-01T10:00:00Z", "old"),
            CreateEvent(customer.Id, InteractionChannel.WhatsApp, InteractionDirection.Outbound, "2026-01-02T10:00:00Z", "mid"),
            CreateEvent(customer.Id, InteractionChannel.LiveChat, InteractionDirection.Inbound, "2026-01-03T10:00:00Z", "new")
        ]);

        using var request = NewAgentRequest(
            HttpMethod.Get,
            $"/api/customers/{customer.Id}/interaction-history?page=1&pageSize=2");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CustomerInteractionHistoryResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.Page);
        Assert.Equal(2, payload.PageSize);
        Assert.Equal(3, payload.TotalCount);
        Assert.Equal(2, payload.Items.Count);
        Assert.Equal("new", payload.Items[0].Summary);
        Assert.Equal("mid", payload.Items[1].Summary);
    }

    [Fact]
    public async Task GetInteractionHistory_AppliesChannelAndDirectionFilters()
    {
        var customer = await CreateCustomerAsync("Nour", "Wingtip");
        await SeedInteractionEventsAsync(customer.Id,
        [
            CreateEvent(customer.Id, InteractionChannel.Email, InteractionDirection.Inbound, "2026-02-01T09:00:00Z", "email in"),
            CreateEvent(customer.Id, InteractionChannel.Email, InteractionDirection.Outbound, "2026-02-01T10:00:00Z", "email out"),
            CreateEvent(customer.Id, InteractionChannel.Sms, InteractionDirection.Inbound, "2026-02-01T11:00:00Z", "sms in")
        ]);

        using var request = NewAgentRequest(
            HttpMethod.Get,
            $"/api/customers/{customer.Id}/interaction-history?channel=email&direction=inbound");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CustomerInteractionHistoryResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.TotalCount);
        Assert.Single(payload.Items);
        Assert.Equal("email", payload.Items[0].Channel);
        Assert.Equal("inbound", payload.Items[0].Direction);
        Assert.Equal("email in", payload.Items[0].Summary);
    }

    [Fact]
    public async Task GetInteractionHistory_ReturnsBadRequest_ForInvalidFilter()
    {
        var customer = await CreateCustomerAsync("Mona", "Northwind");

        using var request = NewAgentRequest(
            HttpMethod.Get,
            $"/api/customers/{customer.Id}/interaction-history?channel=fax");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetInteractionHistory_ReturnsNotFound_WhenCustomerMissing()
    {
        using var request = NewAgentRequest(HttpMethod.Get, $"/api/customers/{Guid.NewGuid()}/interaction-history");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetInteractionHistory_ReturnsUnauthorized_WhenRoleHeaderMissing()
    {
        var customer = await CreateCustomerAsync("Samia", "Litware");
        var response = await _client.GetAsync($"/api/customers/{customer.Id}/interaction-history");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<CustomerProfileResponse> CreateCustomerAsync(string name, string company)
    {
        var email = $"history-{Guid.NewGuid():N}@crm.local";
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

        using var listRequest = NewAgentRequest(HttpMethod.Get, "/api/customers?page=1&pageSize=200");
        var listResponse = await _client.SendAsync(listRequest);
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<CustomerListResponse>();
        Assert.NotNull(listPayload);

        var customer = listPayload!.Items.FirstOrDefault(item => item.ApplicationUserId == userId);
        Assert.NotNull(customer);

        using var profileRequest = NewAgentRequest(HttpMethod.Get, $"/api/customers/{customer!.Id}");
        var profileResponse = await _client.SendAsync(profileRequest);
        profileResponse.EnsureSuccessStatusCode();

        var profile = await profileResponse.Content.ReadFromJsonAsync<CustomerProfileResponse>();
        Assert.NotNull(profile);
        return profile!;
    }

    private async Task SeedInteractionEventsAsync(Guid customerId, IReadOnlyList<CustomerInteractionEvent> events)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        dbContext.CustomerInteractionEvents.AddRange(events);
        await dbContext.SaveChangesAsync();
    }

    private static CustomerInteractionEvent CreateEvent(
        Guid customerId,
        InteractionChannel channel,
        InteractionDirection direction,
        string occurredAtUtc,
        string summary)
    {
        var occurredAt = DateTime.Parse(occurredAtUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        return new CustomerInteractionEvent
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Channel = channel,
            Direction = direction,
            OccurredAtUtc = occurredAt,
            Summary = summary,
            SourceRef = $"src-{Guid.NewGuid():N}",
            SourceSystem = "channels",
            ProjectedAtUtc = DateTime.UtcNow
        };
    }

    private static HttpRequestMessage NewAgentJsonRequest(HttpMethod method, string uri, object body)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-User-Role", "agent");
        request.Content = JsonContent.Create(body);
        return request;
    }

    private static HttpRequestMessage NewAgentRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-User-Role", "agent");
        return request;
    }
}
