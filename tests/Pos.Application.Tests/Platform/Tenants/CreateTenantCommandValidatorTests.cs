using FluentValidation.TestHelper;
using Pos.Application.Platform.Tenants.Commands.CreateTenant;
using Xunit;

namespace Pos.Application.Tests.Platform.Tenants;

public class CreateTenantCommandValidatorTests
{
    private readonly CreateTenantCommandValidator _validator = new();

    [Theory]
    [InlineData("short", "La contraseña del administrador debe tener al menos 8 caracteres.")]
    [InlineData("nopassword1!", "La contraseña del administrador debe contener al menos una letra mayúscula.")]
    [InlineData("NOPASSWORD1", "La contraseña del administrador debe contener al menos un carácter especial.")]
    [InlineData("NoPassword!", "La contraseña del administrador debe contener al menos un número.")]
    [InlineData("NoPassword123", "La contraseña del administrador debe contener al menos un carácter especial.")]
    public void Validate_WeakPassword_ShouldHaveValidationError(string password, string expectedErrorMessage)
    {
        // Arrange
        var command = new CreateTenantCommand(
            "Empresa Test",
            "20601234567",
            "admin@test.com",
            password);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AdminPassword)
            .WithErrorMessage(expectedErrorMessage);
    }

    [Fact]
    public void Validate_ValidPassword_ShouldNotHaveValidationError()
    {
        // Arrange
        var command = new CreateTenantCommand(
            "Empresa Test",
            "20601234567",
            "admin@test.com",
            "TenantAdmin2026!");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.AdminPassword);
    }
}
