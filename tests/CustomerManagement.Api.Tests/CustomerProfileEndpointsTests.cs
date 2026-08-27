using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.Auth;
using CustomerManagement.Api.Contracts.Customers;
using CustomerManagement.Api.Endpoints.Customers;
using CustomerManagement.Api.Tests.Infrastructure;

namespace CustomerManagement.Api.Tests;

public sealed class CustomerProfileEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;

    public CustomerProfileEndpointsTests(CustomerManagementApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateCustomerProfile_ReturnsBadRequest_WhenCreationIsRequestedOutsideRegistration()
    {
        var request = new CreateCustomerProfileRequest(
            "Aya Hassan",
            "Acme",
            [new CreateCustomerContactDetailRequest(1, "aya@example.com", "Work", true)]);

        using var httpRequest = NewAgentRequest(HttpMethod.Post, "/api/customers", request);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListCustomers_ReturnsOk_WhenAgentRoleIsUsed()
    {
        await RegisterCustomerAsync($"list-{Guid.NewGuid():N}@crm.local", "List User", "Contoso");

        using var httpRequest = NewAgentRequest(HttpMethod.Get, "/api/customers?page=1&pageSize=25");
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CustomerListResponse>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Items);
    }

    [Fact]
    public async Task GetCustomerProfile_ReturnsOk_ForExistingCustomer()
    {
        var createdCustomer = await CreateCustomerAsync("Lina", "Globex");
        var customerId = createdCustomer.Id;

        using var httpRequest = NewAgentRequest(HttpMethod.Get, $"/api/customers/{customerId}");
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CustomerProfileResponse>();
        Assert.NotNull(payload);
        Assert.Equal(customerId, payload!.Id);
        Assert.Equal("Lina", payload.Name);
    }

    [Fact]
    public async Task GetCustomerProfile_ReturnsNotFound_ForUnknownCustomer()
    {
        using var httpRequest = NewAgentRequest(HttpMethod.Get, $"/api/customers/{Guid.NewGuid()}");
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCustomerProfile_ReturnsOk_AndUpdatesFields()
    {
        var createdCustomer = await CreateCustomerAsync("Samir", "OldCo");
        var customerId = createdCustomer.Id;
        var updateRequest = new UpdateCustomerProfileRequest(
            "Samir N.",
            "NewCo",
            createdCustomer.RowVersion,
            [
                new CreateCustomerContactDetailRequest(1, "samir@newco.com", "Primary", true),
                new CreateCustomerContactDetailRequest(6, "+201000000000", "Phone", false)
            ]);

        using var updateHttpRequest = NewAgentRequest(HttpMethod.Put, $"/api/customers/{customerId}", updateRequest);
        var updateResponse = await _client.SendAsync(updateHttpRequest);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var payload = await updateResponse.Content.ReadFromJsonAsync<CustomerProfileResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Samir N.", payload!.Name);
        Assert.Equal("NewCo", payload.Company);
        Assert.Equal(2, payload.ContactDetails.Count);
    }

    [Fact]
    public async Task UpdateCustomerProfile_ReturnsConflict_WhenRowVersionIsStale()
    {
        var createdCustomer = await CreateCustomerAsync("Mina", "Tailspin");

        var firstUpdateRequest = new UpdateCustomerProfileRequest(
            "Mina 1",
            "Tailspin",
            createdCustomer.RowVersion,
            null);

        using var firstUpdateHttpRequest = NewAgentRequest(HttpMethod.Put, $"/api/customers/{createdCustomer.Id}", firstUpdateRequest);
        var firstUpdateResponse = await _client.SendAsync(firstUpdateHttpRequest);
        Assert.Equal(HttpStatusCode.OK, firstUpdateResponse.StatusCode);

        var staleUpdateRequest = new UpdateCustomerProfileRequest(
            "Mina 2",
            "Tailspin",
            [1, 2, 3, 4],
            null);

        using var staleUpdateHttpRequest = NewAgentRequest(HttpMethod.Put, $"/api/customers/{createdCustomer.Id}", staleUpdateRequest);
        var staleUpdateResponse = await _client.SendAsync(staleUpdateHttpRequest);

        Assert.Equal(HttpStatusCode.Conflict, staleUpdateResponse.StatusCode);
    }

    [Fact]
    public async Task GetContactDetails_ReturnsOk_ForExistingCustomer()
    {
        var createdCustomer = await CreateCustomerAsync(
            "Nora",
            "Contoso",
            [new CreateCustomerContactDetailRequest(1, "nora@contoso.com", "Work", true)]);
        var customerId = createdCustomer.Id;

        using var httpRequest = NewAgentRequest(HttpMethod.Get, $"/api/customers/{customerId}/contact-details");
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CustomerContactDetailsResponse>();
        Assert.NotNull(payload);
        Assert.Equal(customerId, payload!.CustomerId);
        Assert.Single(payload.ContactDetails);
    }

    [Fact]
    public async Task ProtectedEndpoint_ReturnsUnauthorized_WhenRoleHeaderMissing()
    {
        var response = await _client.PostAsJsonAsync("/api/customers", new CreateCustomerProfileRequest("A", null, null));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_ReturnsForbidden_WhenRoleIsNotAgent()
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/api/customers")
        {
        };
        httpRequest.Headers.Add("X-User-Role", "customer");

        var response = await _client.SendAsync(httpRequest);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReadEndpoint_ReturnsForbidden_WhenPermissionIsMissing()
    {
        var customer = await CreateCustomerAsync("Noha", "Contoso");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/customers/{customer.Id}");
        request.Headers.Add("X-User-Role", "customer");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<CustomerProfileResponse> CreateCustomerAsync(
        string name,
        string? company,
        IReadOnlyList<CreateCustomerContactDetailRequest>? contactDetails = null)
    {
        var email = $"customer-{Guid.NewGuid():N}@crm.local";
        var userId = await RegisterCustomerAsync(email, name, company ?? "Contoso", contactDetails);

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

    private async Task<Guid> RegisterCustomerAsync(
        string email,
        string fullName,
        string company,
        IReadOnlyList<CreateCustomerContactDetailRequest>? contactDetails = null)
    {
        var registrationContacts = (contactDetails ?? [new CreateCustomerContactDetailRequest(1, email, "work", true)])
            .Select(detail => new RegisterContactDetailRequest(detail.Channel, detail.Value, detail.Label, detail.IsPrimary))
            .ToList();

        var registerRequest = new RegisterRequest(
            email,
            "Agent!23456",
            "Agent!23456",
            fullName,
            "customer",
            fullName,
            company,
            registrationContacts);

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        var token = await registerResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(token);
        return Guid.Parse(token!.UserId);
    }

    private static HttpRequestMessage NewAgentRequest(HttpMethod method, string uri, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-User-Role", "agent");

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }
}
