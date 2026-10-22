using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Pos.IntegrationTests;

[Collection("IntegrationTests")]
public class SecurityStartupTests
{
    [Fact]
    public void App_FailsToStart_WhenJwtSettingsAreInvalid()
    {
        var config = new ConfigurationBuilder().Build();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        Action action = () => Pos.Api.Extensions.AuthenticationExtensions.AddCustomAuthentication(services, config);
        action.Should().Throw<InvalidOperationException>().WithMessage("*JwtSettings*");
    }

    [Fact]
    public void App_FailsToStart_WhenCorsSettingsAreMissing()
    {
        var config = new ConfigurationBuilder().Build();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        Action action = () => Pos.Api.Extensions.CorsExtensions.AddCustomCors(services, config);
        action.Should().Throw<InvalidOperationException>().WithMessage("*CorsSettings:AllowedOrigins*");
    }

    [Fact]
    public void App_FailsToStart_WhenOutboxSettingsAreInvalid()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["OutboxSettings:PollingIntervalSeconds"] = "0", // Inválido: Range(1, 3600)
            ["OutboxSettings:BatchSize"] = "-5"              // Inválido: Range(1, 1000)
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddOptions<Pos.Api.Options.OutboxSettings>()
            .Bind(config.GetSection(Pos.Api.Options.OutboxSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var provider = services.BuildServiceProvider();

        Action action = () => { _ = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Pos.Api.Options.OutboxSettings>>().Value; };
        action.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>();
    }

    [Fact]
    public void App_FailsToStart_WhenInvoiceReconciliationSettingsAreInvalid()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["InvoiceReconciliation:IntervalSeconds"] = "0",   // Inválido: Range(5, 86400)
            ["InvoiceReconciliation:ThresholdMinutes"] = "0"  // Inválido: Range(1, 1440)
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddOptions<Pos.Api.Options.InvoiceReconciliationSettings>()
            .Bind(config.GetSection("InvoiceReconciliation"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var provider = services.BuildServiceProvider();

        Action action = () => { _ = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Pos.Api.Options.InvoiceReconciliationSettings>>().Value; };
        action.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>();
    }

    [Fact]
    public void App_FailsToStart_WhenSuperAdminPasswordIsMissing()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["SuperAdminSettings:Email"] = "admin@enterprise.com",
            ["SuperAdminSettings:Password"] = "" // Missing or empty
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddOptions<Pos.Api.Options.SuperAdminSettings>()
            .Bind(config.GetSection(Pos.Api.Options.SuperAdminSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var provider = services.BuildServiceProvider();

        Action action = () => { _ = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Pos.Api.Options.SuperAdminSettings>>().Value; };
        action.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>();
    }

    [Fact]
    public void App_FailsToStart_WhenDatabaseInitializationFails()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
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

                builder.ConfigureTestServices(services =>
                {
                    services.AddScoped<Pos.Infrastructure.Persistence.Seed.SuperAdminSeeder>(sp =>
                        throw new InvalidOperationException("Fallo simulado en DatabaseInitializer"));
                });
            });

        Action act = () =>
        {
            _ = factory.CreateClient();
        };

        act.Should().Throw<Exception>()
            .Where(ex => ex.Message.Contains("Fallo simulado en DatabaseInitializer") ||
                         (ex.InnerException != null && ex.InnerException.Message.Contains("Fallo simulado en DatabaseInitializer")));
    }
}

