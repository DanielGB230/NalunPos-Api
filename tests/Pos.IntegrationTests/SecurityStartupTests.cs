using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Pos.IntegrationTests;

[Collection("Integration")]
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
}
