using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Xunit;

namespace Pos.Domain.Tests;

public class RoleTests
{
    [Fact]
    public void CreateRoleShouldInstantiateRoleAndPermissions()
    {
        // Arrange
        string name = "Administrador POS";
        var permissions = new List<string> { "Sales.Create", "Inventory.View" };

        // Act
        var role = Role.Create(name, "Rol con acceso total", permissions);

        // Assert
        Assert.NotEqual(Guid.Empty, role.Id);
        Assert.Equal(name, role.Name);
        Assert.Equal(2, role.Permissions.Count);
        Assert.Contains("Sales.Create", role.Permissions);
    }

    [Fact]
    public void CreateRoleWithEmptyNameShouldThrowDomainException()
    {
        // Arrange & Act & Assert
        Assert.Throws<DomainException>(() =>
            Role.Create(""));
    }
}
