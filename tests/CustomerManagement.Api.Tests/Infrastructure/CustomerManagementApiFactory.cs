using CustomerManagement.Api.Infrastructure.Attachments;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CustomerManagement.Api.Tests.Infrastructure;

public sealed class CustomerManagementApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"CustomerManagementTests-{Guid.NewGuid()}";
    private readonly string _attachmentRootPath = Path.Combine(
        Path.GetTempPath(),
        $"crm-attachments-integration-{Guid.NewGuid():N}");

    /// <summary>Exposes the shared in-memory database name so tests can register additional DbContext configuration against the same data.</summary>
    public string DatabaseName => _databaseName;

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

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthDefaults.SchemeName;
                    options.DefaultChallengeScheme = TestAuthDefaults.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthDefaults.SchemeName,
                    _ => { });

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
