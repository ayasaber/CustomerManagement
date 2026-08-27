using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.Auth;
using CustomerManagement.Api.Contracts.Customers;
using CustomerManagement.Api.Endpoints.Customers;
using CustomerManagement.Api.Tests.Infrastructure;

namespace CustomerManagement.Api.Tests;

public sealed class CustomerAttachmentEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;

    public CustomerAttachmentEndpointsTests(CustomerManagementApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UploadAttachment_ReturnsCreated_WhenRequestIsValid()
    {
        var customer = await CreateCustomerAsync("Dalia", "Litware");

        using var request = NewAgentMultipartRequest(
            HttpMethod.Post,
            $"/api/customers/{customer.Id}/attachments",
            "chat-transcript.txt",
            "text/plain",
            "hello from attachment");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CustomerAttachmentResponse>();
        Assert.NotNull(payload);
        Assert.Equal(customer.Id, payload!.CustomerId);
        Assert.Equal("chat-transcript.txt", payload.OriginalFileName);
        Assert.Equal("text/plain", payload.ContentType);
        Assert.True(payload.SizeBytes > 0);
        Assert.False(string.IsNullOrWhiteSpace(payload.StorageKey));
    }

    [Fact]
    public async Task UploadAttachment_ReturnsBadRequest_WhenContentTypeIsNotAllowed()
    {
        var customer = await CreateCustomerAsync("Lamia", "Contoso");

        using var request = NewAgentMultipartRequest(
            HttpMethod.Post,
            $"/api/customers/{customer.Id}/attachments",
            "malware.exe",
            "application/octet-stream",
            "not allowed type");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("contentType", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAttachment_ReturnsBadRequest_WhenFileTooLarge()
    {
        var customer = await CreateCustomerAsync("Maya", "Tailspin");
        var largePayload = new string('A', 10 * 1024 * 1024 + 1);

        using var request = NewAgentMultipartRequest(
            HttpMethod.Post,
            $"/api/customers/{customer.Id}/attachments",
            "too-large.txt",
            "text/plain",
            largePayload);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("maximum allowed size", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAttachment_ReturnsBadRequest_WhenFileMissing()
    {
        var customer = await CreateCustomerAsync("Hossam", "Woodgrove");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/customers/{customer.Id}/attachments");
        request.Headers.Add("X-User-Role", "agent");
        request.Content = new MultipartFormDataContent();

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("file", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAttachment_ReturnsNotFound_WhenCustomerMissing()
    {
        using var request = NewAgentMultipartRequest(
            HttpMethod.Post,
            $"/api/customers/{Guid.NewGuid()}/attachments",
            "call.mp3",
            "text/plain",
            "binary-data");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListAttachments_ReturnsAttachments_InReverseChronologicalOrder()
    {
        var customer = await CreateCustomerAsync("Layla", "Alpine");

        using (var uploadFirst = NewAgentMultipartRequest(
                   HttpMethod.Post,
                   $"/api/customers/{customer.Id}/attachments",
                   "first.txt",
                   "text/plain",
                   "first"))
        {
            var firstResponse = await _client.SendAsync(uploadFirst);
            firstResponse.EnsureSuccessStatusCode();
        }

        using (var uploadSecond = NewAgentMultipartRequest(
                   HttpMethod.Post,
                   $"/api/customers/{customer.Id}/attachments",
                   "second.txt",
                   "text/plain",
                   "second"))
        {
            var secondResponse = await _client.SendAsync(uploadSecond);
            secondResponse.EnsureSuccessStatusCode();
        }

        using var listRequest = NewAgentRequest(HttpMethod.Get, $"/api/customers/{customer.Id}/attachments");
        var listResponse = await _client.SendAsync(listRequest);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var attachments = await listResponse.Content.ReadFromJsonAsync<List<CustomerAttachmentResponse>>();
        Assert.NotNull(attachments);
        Assert.Equal(2, attachments!.Count);
        Assert.Equal("second.txt", attachments[0].OriginalFileName);
        Assert.Equal("first.txt", attachments[1].OriginalFileName);
    }

    [Fact]
    public async Task ListAttachments_ReturnsNotFound_WhenCustomerMissing()
    {
        using var request = NewAgentRequest(HttpMethod.Get, $"/api/customers/{Guid.NewGuid()}/attachments");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DownloadAttachment_ReturnsFile_WhenAttachmentExistsForCustomer()
    {
        var customer = await CreateCustomerAsync("Rana", "Northwind");
        var uploaded = await UploadAttachmentAsync(customer.Id, "contract.pdf", "application/pdf", "pdf-bytes");

        using var request = NewAgentRequest(
            HttpMethod.Get,
            $"/api/customers/{customer.Id}/attachments/{uploaded.Id}/content");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Equal("pdf-bytes", payload);
    }

    [Fact]
    public async Task DownloadAttachment_ReturnsNotFound_WhenAttachmentBelongsToDifferentCustomer()
    {
        var customerOne = await CreateCustomerAsync("Alya", "Fabrikam");
        var customerTwo = await CreateCustomerAsync("Noor", "Wingtip");
        var uploaded = await UploadAttachmentAsync(customerOne.Id, "internal.txt", "text/plain", "secret");

        using var request = NewAgentRequest(
            HttpMethod.Get,
            $"/api/customers/{customerTwo.Id}/attachments/{uploaded.Id}/content");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DownloadAttachment_ReturnsNotFound_WhenAttachmentIdMissing()
    {
        var customer = await CreateCustomerAsync("Salma", "Alpine");

        using var request = NewAgentRequest(
            HttpMethod.Get,
            $"/api/customers/{customer.Id}/attachments/{Guid.NewGuid()}/content");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<CustomerProfileResponse> CreateCustomerAsync(string name, string company)
    {
        var email = $"attach-{Guid.NewGuid():N}@crm.local";
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

    private static HttpRequestMessage NewAgentMultipartRequest(
        HttpMethod method,
        string uri,
        string fileName,
        string contentType,
        string content)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-User-Role", "agent");

        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        request.Content = multipart;

        return request;
    }

    private static HttpRequestMessage NewAgentJsonRequest(HttpMethod method, string uri, object body)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-User-Role", "agent");
        request.Content = JsonContent.Create(body);
        return request;
    }

    private async Task<CustomerAttachmentResponse> UploadAttachmentAsync(
        Guid customerId,
        string fileName,
        string contentType,
        string content)
    {
        using var request = NewAgentMultipartRequest(
            HttpMethod.Post,
            $"/api/customers/{customerId}/attachments",
            fileName,
            contentType,
            content);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CustomerAttachmentResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private static HttpRequestMessage NewAgentRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-User-Role", "agent");
        return request;
    }
}
