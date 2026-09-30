using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.TestHost;

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
                ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=NalunPos_IntegrationTestsDb_App;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;",
                ["SuperAdminSettings:Email"] = "superadmin@test.com",
                ["SuperAdminSettings:Password"] = "TestPassword123!",
                ["SuperAdminSettings:FirstName"] = "Super",
                ["SuperAdminSettings:LastName"] = "Admin",
                ["JwtSettings:Secret"] = "SuperSecretEnterpriseJwtKey_LongEnoughFor256Bits_NalunPos2026!",
                ["JwtSettings:Issuer"] = "NalunPosApi",
                ["JwtSettings:Audience"] = "NalunPosClients",
                ["CorsSettings:AllowedOrigins:0"] = "http://localhost:4200"
            });
        });
    }
}
