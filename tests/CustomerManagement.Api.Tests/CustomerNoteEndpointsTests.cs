using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.Auth;
using CustomerManagement.Api.Contracts.Customers;
using CustomerManagement.Api.Endpoints.Customers;
using CustomerManagement.Api.Tests.Infrastructure;

namespace CustomerManagement.Api.Tests;

public sealed class CustomerNoteEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;

    public CustomerNoteEndpointsTests(CustomerManagementApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateNote_ReturnsCreated_WhenRequestIsValid()
    {
        var customer = await CreateCustomerAsync("Nadia", "Wingtip");
        var createNoteRequest = new CreateCustomerNoteRequest("Called customer and confirmed details.");

        using var request = NewAgentRequest(HttpMethod.Post, $"/api/customers/{customer.Id}/notes", createNoteRequest);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CustomerNoteResponse>();
        Assert.NotNull(payload);
        Assert.Equal(customer.Id, payload!.CustomerId);
        Assert.Equal("Called customer and confirmed details.", payload.Body);
    }

    [Fact]
    public async Task CreateNote_ReturnsBadRequest_WhenBodyIsEmpty()
    {
        var customer = await CreateCustomerAsync("Fady", "Contoso");
        var createNoteRequest = new CreateCustomerNoteRequest(" ");

        using var request = NewAgentRequest(HttpMethod.Post, $"/api/customers/{customer.Id}/notes", createNoteRequest);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("body", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListNotes_ReturnsNotes_InReverseChronologicalOrder()
    {
        var customer = await CreateCustomerAsync("Hani", "Northwind");

        using (var createFirst = NewAgentRequest(
                   HttpMethod.Post,
                   $"/api/customers/{customer.Id}/notes",
                   new CreateCustomerNoteRequest("First note")))
        {
            var firstResponse = await _client.SendAsync(createFirst);
            firstResponse.EnsureSuccessStatusCode();
        }

        using (var createSecond = NewAgentRequest(
                   HttpMethod.Post,
                   $"/api/customers/{customer.Id}/notes",
                   new CreateCustomerNoteRequest("Second note")))
        {
            var secondResponse = await _client.SendAsync(createSecond);
            secondResponse.EnsureSuccessStatusCode();
        }

        using var listRequest = NewAgentRequest(HttpMethod.Get, $"/api/customers/{customer.Id}/notes");
        var listResponse = await _client.SendAsync(listRequest);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var notes = await listResponse.Content.ReadFromJsonAsync<List<CustomerNoteResponse>>();
        Assert.NotNull(notes);
        Assert.Equal(2, notes!.Count);
        Assert.Equal("Second note", notes[0].Body);
        Assert.Equal("First note", notes[1].Body);
    }

    [Fact]
    public async Task CreateAndListNotes_ReturnNotFound_WhenCustomerDoesNotExist()
    {
        var missingCustomerId = Guid.NewGuid();

        using var createRequest = NewAgentRequest(
            HttpMethod.Post,
            $"/api/customers/{missingCustomerId}/notes",
            new CreateCustomerNoteRequest("note"));
        var createResponse = await _client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.NotFound, createResponse.StatusCode);

        using var listRequest = NewAgentRequest(HttpMethod.Get, $"/api/customers/{missingCustomerId}/notes");
        var listResponse = await _client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.NotFound, listResponse.StatusCode);
    }

    private async Task<CustomerProfileResponse> CreateCustomerAsync(string name, string company)
    {
        var email = $"notes-{Guid.NewGuid():N}@crm.local";
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
