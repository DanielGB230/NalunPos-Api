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
            // Leer la connection string desde la fuente única de configuración de tests
            var testConfig = new ConfigurationBuilder()
                .AddJsonFile("appsettings.IntegrationTests.json", optional: false)
                .Build();

            var appConnString = testConfig["IntegrationTests:AppConnectionString"]
                ?? throw new InvalidOperationException("IntegrationTests:AppConnectionString no está configurado en appsettings.IntegrationTests.json");

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = appConnString,
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
