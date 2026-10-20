using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
        services.AddOptions<Pos.Api.BackgroundServices.OutboxSettings>()
            .Bind(config.GetSection(Pos.Api.BackgroundServices.OutboxSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var provider = services.BuildServiceProvider();

        Action action = () => { _ = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Pos.Api.BackgroundServices.OutboxSettings>>().Value; };
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
        services.AddOptions<Pos.Api.BackgroundServices.InvoiceReconciliationSettings>()
            .Bind(config.GetSection("InvoiceReconciliation"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var provider = services.BuildServiceProvider();

        Action action = () => { _ = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Pos.Api.BackgroundServices.InvoiceReconciliationSettings>>().Value; };
        action.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>();
    }
}
