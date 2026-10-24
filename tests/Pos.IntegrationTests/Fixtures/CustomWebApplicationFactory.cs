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
        builder.UseSetting("ConnectionStrings:DefaultConnection", "");
        builder.UseSetting("SuperAdminSettings:Email", "superadmin@test.com");
        builder.UseSetting("SuperAdminSettings:Password", "TestPassword123!");
        builder.UseSetting("SuperAdminSettings:FirstName", "Super");
        builder.UseSetting("SuperAdminSettings:LastName", "Admin");
        builder.UseSetting("JwtSettings:Secret", "SuperSecretEnterpriseJwtKey_LongEnoughFor256Bits_NalunPos2026!");
        builder.UseSetting("JwtSettings:Issuer", "NalunPosApi");
        builder.UseSetting("JwtSettings:Audience", "NalunPosClients");
        builder.UseSetting("CorsSettings:AllowedOrigins:0", "http://localhost:4200");
        builder.UseSetting("RateLimiting:AuthPolicy:PermitLimit", "5");
        builder.UseSetting("RateLimiting:AuthPolicy:WindowMinutes", "1");
        builder.UseSetting("RateLimiting:AuthPolicy:QueueLimit", "0");
        builder.UseSetting("RateLimiting:PosCheckoutPolicy:PermitLimit", "5");
        builder.UseSetting("RateLimiting:PosCheckoutPolicy:WindowMinutes", "1");
        builder.UseSetting("RateLimiting:PosCheckoutPolicy:SegmentsPerWindow", "2");
        builder.UseSetting("RateLimiting:PosCheckoutPolicy:QueueLimit", "0");
        builder.UseSetting("RateLimiting:SensitiveOperationsPolicy:PermitLimit", "2");
        builder.UseSetting("RateLimiting:SensitiveOperationsPolicy:WindowMinutes", "1");
        builder.UseSetting("RateLimiting:SensitiveOperationsPolicy:QueueLimit", "0");
        builder.UseSetting("RateLimiting:GlobalApiPolicy:PermitLimit", "3");
        builder.UseSetting("RateLimiting:GlobalApiPolicy:WindowMinutes", "1");
        builder.UseSetting("RateLimiting:GlobalApiPolicy:QueueLimit", "0");

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
            var outboxDescriptor = services.FirstOrDefault(d =>
                d.ImplementationType == typeof(Pos.Api.BackgroundServices.OutboxProcessorBackgroundService));
            if (outboxDescriptor != null)
            {
                services.Remove(outboxDescriptor);
            }

            var reconciliationDescriptor = services.FirstOrDefault(d =>
                d.ImplementationType == typeof(Pos.Api.BackgroundServices.InvoiceReconciliationBackgroundService));
            if (reconciliationDescriptor != null)
            {
                services.Remove(reconciliationDescriptor);
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
                options.AddPosSaveChangesInterceptors(provider);
            });
        });
    }
}
