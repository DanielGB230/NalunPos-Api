using FluentValidation.TestHelper;
using Pos.Application.Roles;
using Pos.Application.Roles.Commands;
using Xunit;

namespace Pos.Application.Tests.Roles;

public class CreateRoleCommandValidatorTests
{
    private readonly CreateRoleCommandValidator _validator = new();

    [Theory]
    [InlineData("superadmin")]
    [InlineData(" SuperAdmin ")]
    [InlineData("TENANTADMIN")]
    [InlineData("Supervisor")]
    [InlineData("InventoryManager")]
    [InlineData("cajero")]
    public void Validate_ReservedRoleName_ShouldFailWithReservedCode(string reservedName)
    {
        // Arrange
        var command = new CreateRoleCommand(reservedName, "Descripción");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorCode(RoleErrorCodes.NameReserved);
    }

    [Theory]
    [InlineData("SupervisorDeInventario")]
    [InlineData("Cajero Auxiliar")]
    [InlineData("Vendedor Senior")]
    public void Validate_CustomValidRoleName_ShouldSucceed(string validName)
    {
        // Arrange
        var command = new CreateRoleCommand(validName, "Descripción válida");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }
}
