using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pos.Infrastructure.Multitenancy;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Interceptors;

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
                ["ConnectionStrings:DefaultConnection"] = "",
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

        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.FirstOrDefault(d =>
                d.ImplementationType == typeof(Pos.Api.BackgroundServices.OutboxProcessorBackgroundService));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<DbContextOptions<PosDbContext>>();
            services.RemoveAll<DbContextOptions>();
            var efConfigs = services
                .Where(d => d.ServiceType.IsGenericType &&
                            d.ServiceType.GetGenericTypeDefinition().FullName == "Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration`1" &&
                            d.ServiceType.GetGenericArguments()[0] == typeof(PosDbContext))
                .ToList();
            foreach (var d in efConfigs) services.Remove(d);

            services.AddDbContext<PosDbContext>((provider, options) =>
            {
                options.UseInMemoryDatabase("NalunPosDb_Test");
                options.AddInterceptors(
                    provider.GetRequiredService<TenantSaveChangesInterceptor>(),
                    provider.GetRequiredService<AuditSaveChangesInterceptor>(),
                    provider.GetRequiredService<InsertOutboxMessagesInterceptor>()
                );
            });
        });
    }
}
