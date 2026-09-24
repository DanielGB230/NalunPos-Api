using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Pos.IntegrationTests.Fixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SuperAdminSettings:Email"] = "superadmin@test.com",
                ["SuperAdminSettings:Password"] = "TestPassword123!",
                ["SuperAdminSettings:FirstName"] = "Super",
                ["SuperAdminSettings:LastName"] = "Admin",
                ["JwtSettings:Secret"] = "SuperSecretEnterpriseJwtKey_LongEnoughFor256Bits_NalunPos2026!",
                ["JwtSettings:Issuer"] = "NalunPosApi",
                ["JwtSettings:Audience"] = "NalunPosClients"
            });
        });
    }
}
