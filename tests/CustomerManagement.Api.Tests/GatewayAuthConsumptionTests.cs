using System.Net;
using CustomerManagement.Api.Infrastructure.Attachments;
using CustomerManagement.Api.Infrastructure.Persistence;
using CustomerManagement.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CustomerManagement.Api.Tests;

public sealed class GatewayAuthConsumptionTests : IClassFixture<JwtOnlyApiFactory>
{
    private readonly HttpClient _client;

    public GatewayAuthConsumptionTests(JwtOnlyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProtectedApi_DoesNotAcceptClientSuppliedRoleHeader_WithoutJwt()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/customers/{Guid.NewGuid()}");
        request.Headers.Add(TestAuthDefaults.RoleHeader, "agent");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public sealed class JwtOnlyApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"CustomerManagementJwtOnlyTests-{Guid.NewGuid()}";
    private readonly string _attachmentRootPath = Path.Combine(
        Path.GetTempPath(),
        $"crm-attachments-jwt-only-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CustomerManagementDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CustomerManagementDbContext>>();
            services.AddDbContext<CustomerManagementDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IConfigureOptions<AttachmentStorageOptions>>();
            services.Configure<AttachmentStorageOptions>(options =>
            {
                options.RootPath = _attachmentRootPath;
            });

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();
            dbContext.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_attachmentRootPath))
        {
            Directory.Delete(_attachmentRootPath, recursive: true);
        }
    }
}
