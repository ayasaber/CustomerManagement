using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using CustomerManagement.Api.Contracts.Admin.Settings;
using CustomerManagement.Api.Contracts.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using CustomerManagement.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerManagement.Api.Tests;

public sealed class SystemSettingsEndpointsTests : IClassFixture<CustomerManagementApiFactory>
{
    private readonly HttpClient _client;
    private readonly CustomerManagementApiFactory _factory;

    public SystemSettingsEndpointsTests(CustomerManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListGetAndUpdate_ReturnExpectedResults_AndAuditIsWritten()
    {
        using var listRequest = NewAdminRequest(HttpMethod.Get, "/api/admin/system-settings");
        var listResponse = await _client.SendAsync(listRequest);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listPayload = await listResponse.Content.ReadFromJsonAsync<List<SystemSettingResponse>>();
        Assert.NotNull(listPayload);
        Assert.NotEmpty(listPayload!);

        var target = listPayload!.First(setting => setting.Key == "Auth.AccessTokenMinutes");

        using var getRequest = NewAdminRequest(HttpMethod.Get, $"/api/admin/system-settings/{Uri.EscapeDataString(target.Key)}");
        var getResponse = await _client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getPayload = await getResponse.Content.ReadFromJsonAsync<SystemSettingResponse>();
        Assert.NotNull(getPayload);

        using var updateRequest = NewAdminJsonRequest(
            HttpMethod.Put,
            $"/api/admin/system-settings/{Uri.EscapeDataString(target.Key)}",
            new UpdateSystemSettingRequest("45", getPayload!.RowVersion));

        var updateResponse = await _client.SendAsync(updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<SystemSettingResponse>();
        Assert.NotNull(updated);
        Assert.Equal("45", updated!.Value);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();
        var auditRow = await dbContext.AuditLogEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(entry =>
                entry.ActionType == "admin.system-setting.update" &&
                entry.EntityId == "Auth.AccessTokenMinutes");

        Assert.NotNull(auditRow);
        Assert.Equal("Success", auditRow!.Result);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenRowVersionIsStale()
    {
        var setting = await GetSettingAsync("Auth.RefreshTokenDays");

        using var firstUpdateRequest = NewAdminJsonRequest(
            HttpMethod.Put,
            "/api/admin/system-settings/Auth.RefreshTokenDays",
            new UpdateSystemSettingRequest("21", setting.RowVersion));
        var firstUpdateResponse = await _client.SendAsync(firstUpdateRequest);
        Assert.Equal(HttpStatusCode.OK, firstUpdateResponse.StatusCode);

        using var staleUpdateRequest = NewAdminJsonRequest(
            HttpMethod.Put,
            "/api/admin/system-settings/Auth.RefreshTokenDays",
            new UpdateSystemSettingRequest("30", setting.RowVersion));
        var staleUpdateResponse = await _client.SendAsync(staleUpdateRequest);

        Assert.Equal(HttpStatusCode.Conflict, staleUpdateResponse.StatusCode);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenNumericValueIsInvalid()
    {
        var setting = await GetSettingAsync("Auth.AccessTokenMinutes");

        using var updateRequest = NewAdminJsonRequest(
            HttpMethod.Put,
            "/api/admin/system-settings/Auth.AccessTokenMinutes",
            new UpdateSystemSettingRequest("not-a-number", setting.RowVersion));
        var response = await _client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var current = await GetSettingAsync("Auth.AccessTokenMinutes");
        Assert.Equal(setting.Value, current.Value);
    }

    [Fact]
    public async Task AuthTokenExpiries_TrackUpdatedSettings()
    {
        await UpdateSettingAsync("Auth.AccessTokenMinutes", "2");
        await UpdateSettingAsync("Auth.RefreshTokenDays", "3");

        var email = $"settings-auth-{Guid.NewGuid():N}@crm.local";
        const string password = "Agent!23456789";

        var registerResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, password, password, "Settings Auth User", "agent"));

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var tokens = await registerResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokens);

        var accessLifetime = tokens!.AccessTokenExpiresAtUtc - DateTime.UtcNow;
        var refreshLifetime = tokens.RefreshTokenExpiresAtUtc - DateTime.UtcNow;

        Assert.InRange(accessLifetime.TotalMinutes, 1.0, 3.0);
        Assert.InRange(refreshLifetime.TotalDays, 2.0, 4.0);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);
        Assert.True(jwt.ValidTo > DateTime.MinValue);
    }

    [Fact]
    public async Task Register_UsesPasswordMinLengthSetting()
    {
        await UpdateSettingAsync("Auth.Password.MinLength", "12");

        var weakRequest = new RegisterRequest(
            $"weak-{Guid.NewGuid():N}@crm.local",
            "Agent!2345",
            "Agent!2345",
            "Weak User",
            "agent");

        var weakResponse = await _client.PostAsJsonAsync("/api/auth/register", weakRequest);
        Assert.Equal(HttpStatusCode.BadRequest, weakResponse.StatusCode);

        var strongPassword = "Agent!2345678";
        var strongRequest = weakRequest with
        {
            Email = $"strong-{Guid.NewGuid():N}@crm.local",
            Password = strongPassword,
            ConfirmPassword = strongPassword
        };

        var strongResponse = await _client.PostAsJsonAsync("/api/auth/register", strongRequest);
        Assert.Equal(HttpStatusCode.Created, strongResponse.StatusCode);
    }

    private async Task<SystemSettingResponse> GetSettingAsync(string key)
    {
        using var getRequest = NewAdminRequest(HttpMethod.Get, $"/api/admin/system-settings/{Uri.EscapeDataString(key)}");
        var getResponse = await _client.SendAsync(getRequest);
        getResponse.EnsureSuccessStatusCode();

        var payload = await getResponse.Content.ReadFromJsonAsync<SystemSettingResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task UpdateSettingAsync(string key, string value)
    {
        var setting = await GetSettingAsync(key);
        using var updateRequest = NewAdminJsonRequest(
            HttpMethod.Put,
            $"/api/admin/system-settings/{Uri.EscapeDataString(key)}",
            new UpdateSystemSettingRequest(value, setting.RowVersion));
        var response = await _client.SendAsync(updateRequest);
        response.EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage NewAdminRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(TestAuthDefaults.RoleHeader, "admin");
        return request;
    }

    private static HttpRequestMessage NewAdminJsonRequest(HttpMethod method, string uri, object body)
    {
        var request = NewAdminRequest(method, uri);
        request.Content = JsonContent.Create(body);
        return request;
    }
}
