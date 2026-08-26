using CustomerManagement.Gateway.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CustomerManagement.Gateway.Tests.Infrastructure;

public sealed class CustomerManagementGatewayFactory : WebApplicationFactory<Program>
{
    private readonly int _downstreamPort;

    public CustomerManagementGatewayFactory(int downstreamPort)
    {
        _downstreamPort = downstreamPort;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["GlobalConfiguration:BaseUrl"] = "http://localhost:5101",
                ["Jwt:Issuer"] = TestJwtFactory.Issuer,
                ["Jwt:Audience"] = TestJwtFactory.Audience,
                ["Jwt:SigningKey"] = TestJwtFactory.SigningKey
            };

            for (var i = 0; i < 23; i++)
            {
                overrides[$"Routes:{i}:DownstreamScheme"] = "http";
                overrides[$"Routes:{i}:DownstreamHostAndPorts:0:Host"] = "127.0.0.1";
                overrides[$"Routes:{i}:DownstreamHostAndPorts:0:Port"] = _downstreamPort.ToString();
            }

            config.AddInMemoryCollection(overrides);
        });
    }
}
