using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.Auth;
using CustomerManagement.Api.Contracts.Customers;
using CustomerManagement.Api.Contracts.Tickets;
using CustomerManagement.Api.Endpoints.Customers;
using CustomerManagement.Api.Tests.Infrastructure;

namespace CustomerManagement.Api.Tests;

public sealed class TicketEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;

    public TicketEndpointsTests(CustomerManagementApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CategoryAndPriority_CanBeCreatedAndListed()
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N");
        var categoryCreate = new CreateTicketCategoryRequest($"Billing-{uniqueSuffix}", "Billing issues");
        using var createCategoryRequest = NewAdminRequest(HttpMethod.Post, "/api/tickets/categories", categoryCreate);
        var createCategoryResponse = await _client.SendAsync(createCategoryRequest);
        Assert.Equal(HttpStatusCode.Created, createCategoryResponse.StatusCode);

        var category = await createCategoryResponse.Content.ReadFromJsonAsync<TicketCategoryResponse>();
        Assert.NotNull(category);

        var priorityCreate = new CreateTicketPriorityRequest($"High-{uniqueSuffix}", 110);
        using var createPriorityRequest = NewAdminRequest(HttpMethod.Post, "/api/tickets/priorities", priorityCreate);
        var createPriorityResponse = await _client.SendAsync(createPriorityRequest);
        Assert.Equal(HttpStatusCode.Created, createPriorityResponse.StatusCode);

        var priority = await createPriorityResponse.Content.ReadFromJsonAsync<TicketPriorityResponse>();
        Assert.NotNull(priority);

        using var listCategoriesRequest = NewAgentRequest(HttpMethod.Get, "/api/tickets/categories");
        var listCategoriesResponse = await _client.SendAsync(listCategoriesRequest);
        Assert.Equal(HttpStatusCode.OK, listCategoriesResponse.StatusCode);

        var categories = await listCategoriesResponse.Content.ReadFromJsonAsync<List<TicketCategoryResponse>>();
        Assert.NotNull(categories);
        Assert.Contains(categories!, row => row.Id == category!.Id && row.Name == categoryCreate.Name);

        using var listPrioritiesRequest = NewAgentRequest(HttpMethod.Get, "/api/tickets/priorities");
        var listPrioritiesResponse = await _client.SendAsync(listPrioritiesRequest);
        Assert.Equal(HttpStatusCode.OK, listPrioritiesResponse.StatusCode);

        var priorities = await listPrioritiesResponse.Content.ReadFromJsonAsync<List<TicketPriorityResponse>>();
        Assert.NotNull(priorities);
        Assert.Contains(priorities!, row => row.Id == priority!.Id && row.Name == priorityCreate.Name);
    }

    [Fact]
    public async Task Ticket_CreateGetAndHistory_WorkAsExpected()
    {
        var customer = await CreateCustomerAsync("Ticket User", "Contoso");
        var category = await CreateCategoryAsync("Technical");
        var priority = await CreatePriorityAsync("Medium", 20);

        var createTicketRequest = new CreateTicketRequest(
            customer.ApplicationUserId,
            category.Id,
            priority.Id,
            "Login issue",
            "User cannot sign in after reset.");

        using var createRequest = NewAgentRequest(HttpMethod.Post, "/api/tickets", createTicketRequest);
        var createResponse = await _client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<TicketResponse>();
        Assert.NotNull(created);
        Assert.Equal("new", created!.Status);

        using var getRequest = NewAgentRequest(HttpMethod.Get, $"/api/tickets/{created.Id}");
        var getResponse = await _client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<TicketResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal("Login issue", fetched.Subject);

        using var historyRequest = NewAgentRequest(HttpMethod.Get, $"/api/tickets/{created.Id}/history");
        var historyResponse = await _client.SendAsync(historyRequest);
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        var history = await historyResponse.Content.ReadFromJsonAsync<TicketHistoryResponse>();
        Assert.NotNull(history);
        Assert.Equal(created.Id, history!.TicketId);
        Assert.NotEmpty(history.Items);
    }

    [Fact]
    public async Task TicketStatus_ReturnsBadRequest_ForInvalidTransition()
    {
        var customer = await CreateCustomerAsync("Transition User", "Contoso");
        var category = await CreateCategoryAsync($"General-{Guid.NewGuid():N}");
        var priority = await CreatePriorityAsync($"Low-{Guid.NewGuid():N}", 30);

        var createTicketRequest = new CreateTicketRequest(
            customer.ApplicationUserId,
            category.Id,
            priority.Id,
            "Need update",
            "Need an account update.");

        using var createRequest = NewAgentRequest(HttpMethod.Post, "/api/tickets", createTicketRequest);
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<TicketResponse>();
        Assert.NotNull(created);

        var invalidTransition = new UpdateTicketStatusRequest("closed", created!.RowVersion);
        using var transitionRequest = NewAgentRequest(HttpMethod.Put, $"/api/tickets/{created.Id}/status", invalidTransition);
        var transitionResponse = await _client.SendAsync(transitionRequest);

        Assert.Equal(HttpStatusCode.BadRequest, transitionResponse.StatusCode);
    }

    private async Task<CustomerProfileResponse> CreateCustomerAsync(string name, string company)
    {
        var email = $"customer-{Guid.NewGuid():N}@crm.local";
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

        using var getRequest = NewAgentRequest(HttpMethod.Get, "/api/customers?page=1&pageSize=100");
        var listResponse = await _client.SendAsync(getRequest);
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

    private async Task<TicketCategoryResponse> CreateCategoryAsync(string name)
    {
        var request = new CreateTicketCategoryRequest(name, null);
        using var httpRequest = NewAdminRequest(HttpMethod.Post, "/api/tickets/categories", request);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TicketCategoryResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task<TicketPriorityResponse> CreatePriorityAsync(string name, int sortOrder)
    {
        var request = new CreateTicketPriorityRequest(name, sortOrder);
        using var httpRequest = NewAdminRequest(HttpMethod.Post, "/api/tickets/priorities", request);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TicketPriorityResponse>();
        Assert.NotNull(payload);
        return payload!;
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

    private static HttpRequestMessage NewAdminRequest(HttpMethod method, string uri, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-User-Role", "admin");

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }
}
