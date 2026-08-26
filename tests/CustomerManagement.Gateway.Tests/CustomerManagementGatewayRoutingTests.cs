using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Gateway.Tests.Infrastructure;

namespace CustomerManagement.Gateway.Tests;

public sealed class CustomerManagementGatewayRoutingTests : IAsyncLifetime
{
    private DownstreamStubServer _stub = null!;
    private CustomerManagementGatewayFactory _factory = null!;
    private HttpClient _gatewayClient = null!;

    public async Task InitializeAsync()
    {
        _stub = await DownstreamStubServer.StartAsync();
        _factory = new CustomerManagementGatewayFactory(_stub.Port);
        _gatewayClient = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _gatewayClient.Dispose();
        _factory.Dispose();
        await _stub.DisposeAsync();
    }

    [Theory]
    [InlineData("GET", "/api/customers/11111111-1111-1111-1111-111111111111")]
    [InlineData("PUT", "/api/customers/11111111-1111-1111-1111-111111111111")]
    [InlineData("GET", "/api/customers/11111111-1111-1111-1111-111111111111/contact-details")]
    [InlineData("GET", "/api/customers/11111111-1111-1111-1111-111111111111/notes")]
    [InlineData("POST", "/api/customers/11111111-1111-1111-1111-111111111111/notes")]
    [InlineData("GET", "/api/customers/11111111-1111-1111-1111-111111111111/attachments")]
    [InlineData("POST", "/api/customers/11111111-1111-1111-1111-111111111111/attachments")]
    [InlineData("GET", "/api/customers/11111111-1111-1111-1111-111111111111/attachments/22222222-2222-2222-2222-222222222222/content")]
    [InlineData("GET", "/api/customers/11111111-1111-1111-1111-111111111111/interaction-history?page=2&pageSize=10&channel=email&direction=inbound")]
    public async Task Gateway_RoutesCustomerManagementRequests_ToDownstream(string method, string pathAndQuery)
    {
        using var request = NewAuthorizedRequest(
            new HttpMethod(method),
            pathAndQuery,
            new { name = "Aya" },
            permissions: ["customers.read", "customers.write"]);
        var response = await _gatewayClient.SendAsync(request);

        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var payload = await response.Content.ReadFromJsonAsync<GatewayEchoResponse>();
            Assert.NotNull(payload);
            Assert.Equal(method, payload!.Method, ignoreCase: true);
            Assert.Equal(payload.UserId, payload.ForwardedUserId);
            Assert.Equal(payload.Email, payload.ForwardedEmail);
            Assert.Contains("/api/customers", payload.Path, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("GET", "/api/tickets")]
    [InlineData("POST", "/api/tickets")]
    [InlineData("GET", "/api/tickets/11111111-1111-1111-1111-111111111111")]
    [InlineData("PUT", "/api/tickets/11111111-1111-1111-1111-111111111111")]
    [InlineData("PUT", "/api/tickets/11111111-1111-1111-1111-111111111111/status")]
    [InlineData("PUT", "/api/tickets/11111111-1111-1111-1111-111111111111/assign")]
    [InlineData("PUT", "/api/tickets/11111111-1111-1111-1111-111111111111/self-assign")]
    [InlineData("POST", "/api/tickets/11111111-1111-1111-1111-111111111111/escalate")]
    [InlineData("POST", "/api/tickets/11111111-1111-1111-1111-111111111111/reopen")]
    [InlineData("GET", "/api/tickets/11111111-1111-1111-1111-111111111111/history")]
    [InlineData("GET", "/api/tickets/categories")]
    [InlineData("POST", "/api/tickets/categories")]
    [InlineData("PUT", "/api/tickets/categories/11111111-1111-1111-1111-111111111111")]
    [InlineData("GET", "/api/tickets/priorities")]
    [InlineData("POST", "/api/tickets/priorities")]
    [InlineData("PUT", "/api/tickets/priorities/11111111-1111-1111-1111-111111111111")]
    public async Task Gateway_RoutesTicketManagementRequests_ToDownstream(string method, string pathAndQuery)
    {
        using var request = NewAuthorizedRequest(
            new HttpMethod(method),
            pathAndQuery,
            new { subject = "Need help" },
            permissions: ["tickets.read", "tickets.write", "tickets.assign", "tickets.escalate", "tickets.close", "tickets.taxonomy.manage"]);
        var response = await _gatewayClient.SendAsync(request);

        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var payload = await response.Content.ReadFromJsonAsync<GatewayEchoResponse>();
            Assert.NotNull(payload);
            Assert.Equal(method, payload!.Method, ignoreCase: true);
            Assert.Equal(payload.UserId, payload.ForwardedUserId);
            Assert.Equal(payload.Email, payload.ForwardedEmail);
            Assert.Contains("/api/tickets", payload.Path, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Gateway_ForwardsIdentityHeaders_FromJwtClaims()
    {
        using var request = NewAuthorizedRequest(
            HttpMethod.Get,
            "/api/customers/11111111-1111-1111-1111-111111111111",
            permissions: ["customers.read"]);
        var response = await _gatewayClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<GatewayEchoResponse>();
        Assert.NotNull(payload);
        Assert.Equal(payload!.UserId, payload.ForwardedUserId);
        Assert.Equal(payload.Email, payload.ForwardedEmail);
    }

    [Fact]
    public async Task Gateway_ReturnsUnauthorized_WhenBearerTokenIsMissing()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/customers/11111111-1111-1111-1111-111111111111");
        request.Headers.Add("X-User-Role", "agent");

        var response = await _gatewayClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Gateway_ReturnsUnauthorized_WhenBearerTokenIsInvalid()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/customers/11111111-1111-1111-1111-111111111111");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-valid-token");

        var response = await _gatewayClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Gateway_PropagatesForbidden_WhenTokenLacksPermission()
    {
        using var request = NewAuthorizedRequest(
            HttpMethod.Get,
            "/api/customers/11111111-1111-1111-1111-111111111111",
            permissions: ["audit.read"]);
        var response = await _gatewayClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Gateway_PropagatesValidationProblem_ForInvalidPayload()
    {
        using var request = NewAuthorizedRequest(
            HttpMethod.Post,
            "/api/customers",
            new { name = " " },
            permissions: ["customers.read", "customers.write"]);
        var response = await _gatewayClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("name", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Gateway_PropagatesDownstreamNotFound_AndValidationErrors()
    {
        using var notFoundRequest = NewAuthorizedRequest(
            HttpMethod.Get,
            $"/api/customers/{Guid.Empty}/interaction-history",
            permissions: ["customers.read"]);
        var notFoundResponse = await _gatewayClient.SendAsync(notFoundRequest);
        Assert.Equal(HttpStatusCode.NotFound, notFoundResponse.StatusCode);

        using var validationRequest = NewAuthorizedRequest(
            HttpMethod.Get,
            "/api/customers/11111111-1111-1111-1111-111111111111/interaction-history?channel=fax",
            permissions: ["customers.read"]);
        var validationResponse = await _gatewayClient.SendAsync(validationRequest);
        Assert.Equal(HttpStatusCode.BadRequest, validationResponse.StatusCode);
    }

    [Fact]
    public async Task Gateway_PropagatesDownstreamServerError()
    {
        using var request = NewAuthorizedRequest(
            HttpMethod.Get,
            "/api/customers/11111111-1111-1111-1111-111111111111/interaction-history?simulateError=true",
            permissions: ["customers.read"]);

        var response = await _gatewayClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private static HttpRequestMessage NewAuthorizedRequest(
        HttpMethod method,
        string uri,
        object? body = null,
        IEnumerable<string>? permissions = null)
    {
        var request = new HttpRequestMessage(method, uri);
        var token = TestJwtFactory.CreateToken(
            userId: Guid.NewGuid().ToString(),
            email: "agent@crm.local",
            roles: ["agent"],
            permissions: permissions ?? ["customers.read"]);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private sealed record GatewayEchoResponse(
        string Method,
        string Path,
        string Query,
        string UserId,
        string Email,
        string ForwardedUserId,
        string ForwardedEmail);
}
