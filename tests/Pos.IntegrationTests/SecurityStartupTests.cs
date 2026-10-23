namespace Pos.IntegrationTests;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Pos.Infrastructure.Persistence.Seed;
using Xunit;

[Collection("IntegrationTests")]
public class SecurityStartupTests
{
    private static WebApplicationFactory<Program> CreateBaseFactory(Action<IWebHostBuilder>? configure = null)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", "");
                builder.UseSetting("SuperAdminSettings:Email", "admin@enterprise.com");
                builder.UseSetting("SuperAdminSettings:Password", "SuperSecurePassword123!");
                builder.UseSetting("SuperAdminSettings:FirstName", "Super");
                builder.UseSetting("SuperAdminSettings:LastName", "Admin");
                builder.UseSetting("JwtSettings:Secret", "SuperSecretEnterpriseJwtKey_LongEnoughFor256Bits_NalunPos2026!");
                builder.UseSetting("JwtSettings:Issuer", "NalunPosApi");
                builder.UseSetting("JwtSettings:Audience", "NalunPosClients");
                builder.UseSetting("CorsSettings:AllowedOrigins:0", "http://localhost:4200");

                configure?.Invoke(builder);
            });
    }

    [Fact]
    public void App_FailsToStart_WhenSuperAdminPasswordIsMissing()
    {
        using var factory = CreateBaseFactory(builder =>
        {
            builder.UseSetting("SuperAdminSettings:Password", "");
        });

        Action act = () => _ = factory.CreateClient();

        act.Should().Throw<Exception>()
            .Where(ex => ex.ToString().Contains("SuperAdmin") ||
                         ex.ToString().Contains("OptionsValidationException"));
    }

    [Fact]
    public void App_FailsToStart_WhenJwtSettingsAreInvalid()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", "");
                builder.UseSetting("SuperAdminSettings:Email", "admin@enterprise.com");
                builder.UseSetting("SuperAdminSettings:Password", "SuperSecurePassword123!");
                builder.UseSetting("CorsSettings:AllowedOrigins:0", "http://localhost:4200");
                // Sin JwtSettings configurado
            });

        Action act = () => _ = factory.CreateClient();

        act.Should().Throw<Exception>()
            .Where(ex => ex.ToString().Contains("JwtSettings") ||
                         ex.ToString().Contains("Secret"));
    }

    [Fact]
    public void App_FailsToStart_WhenCorsSettingsAreMissing()
    {
        using var factory = CreateBaseFactory(builder =>
        {
            builder.UseSetting("CorsSettings:AllowedOrigins:0", "");
            builder.UseSetting("CorsSettings:AllowedOrigins:1", "");
        });

        Action act = () => _ = factory.CreateClient();

        act.Should().Throw<Exception>()
            .Where(ex => ex.ToString().Contains("CorsSettings:AllowedOrigins"));
    }

    [Fact]
    public void App_FailsToStart_WhenOutboxSettingsAreInvalid()
    {
        using var factory = CreateBaseFactory(builder =>
        {
            builder.UseSetting("OutboxSettings:BatchSize", "-5");
        });

        Action act = () => _ = factory.CreateClient();

        act.Should().Throw<Exception>()
            .Where(ex => ex.ToString().Contains("OutboxSettings") ||
                         ex.ToString().Contains("OptionsValidationException") ||
                         ex.ToString().Contains("BatchSize"));
    }

    [Fact]
    public void App_FailsToStart_WhenInvoiceReconciliationSettingsAreInvalid()
    {
        using var factory = CreateBaseFactory(builder =>
        {
            builder.UseSetting("InvoiceReconciliation:IntervalSeconds", "0");
        });

        Action act = () => _ = factory.CreateClient();

        act.Should().Throw<Exception>()
            .Where(ex => ex.ToString().Contains("InvoiceReconciliation") ||
                         ex.ToString().Contains("OptionsValidationException") ||
                         ex.ToString().Contains("IntervalSeconds"));
    }

    [Fact]
    public void App_FailsToStart_WhenDatabaseInitializationFails()
    {
        using var factory = CreateBaseFactory(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<SuperAdminSeeder>(sp =>
                    throw new InvalidOperationException("Fallo simulado en DatabaseInitializer"));
            });
        });

        Action act = () => _ = factory.CreateClient();

        act.Should().Throw<Exception>()
            .Where(ex => ex.ToString().Contains("Fallo simulado en DatabaseInitializer"));
    }
}
