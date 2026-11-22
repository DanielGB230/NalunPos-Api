using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

/// <summary>
/// Prueba el registro REAL de opciones de <c>Program.cs</c>: al arrancar el host,
/// <c>RefreshToken:FamilyAbsoluteDays &lt; RefreshToken:RefreshTokenDays</c> debe
/// producir una <see cref="OptionsValidationException"/>. La regla vive únicamente
/// en <c>IValidatableObject</c>, ejecutado por <c>ValidateDataAnnotations()</c>.
/// </summary>
[Collection("IntegrationTests")]
public class RefreshTokenOptionsStartupIntegrationTests
{
    [Fact]
    public void App_FailsToStart_WhenFamilyAbsoluteDaysIsLowerThanRefreshTokenDays()
    {
        using var factory = new CustomWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("RefreshToken:FamilyAbsoluteDays", "3");
                builder.UseSetting("RefreshToken:RefreshTokenDays", "7");
            });

        Action act = () => _ = factory.CreateClient();

        Exception exception = act.Should().Throw<Exception>().Which;
        OptionsValidationException? optionsException = FindOptionsValidationException(exception);

        optionsException.Should().NotBeNull(
            "el host real debe validar las opciones al arrancar mediante IValidatableObject");
        optionsException!.Failures.Should().Contain(
            failure => failure.Contains("FamilyAbsoluteDays", StringComparison.Ordinal) &&
                       failure.Contains("RefreshTokenDays", StringComparison.Ordinal));
    }

    private static OptionsValidationException? FindOptionsValidationException(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is OptionsValidationException optionsException)
            {
                return optionsException;
            }

            exception = exception.InnerException;
        }

        return null;
    }
}
