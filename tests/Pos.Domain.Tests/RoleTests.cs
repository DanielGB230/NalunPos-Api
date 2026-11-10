using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Xunit;

namespace Pos.Domain.Tests;

public class RoleTests
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    [Fact]
    public void CreateRoleShouldInstantiateRoleAndPermissions()
    {
        // Arrange
        string name = "Administrador POS";
        var permissions = new List<string> { "Sales.Create", "Inventory.View" };

        // Act
        var role = Role.Create(Guid.NewGuid(), name, "Rol con acceso total", permissions, _timeProvider);

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
            Role.Create(Guid.NewGuid(), "", _timeProvider));
    }

    [Theory]
    [InlineData("superadmin")]
    [InlineData(" SuperAdmin ")]
    [InlineData("TENANTADMIN")]
    [InlineData("Supervisor")]
    [InlineData("inventorymanager")]
    [InlineData("  cajero  ")]
    public void Create_WithReservedRoleName_ShouldThrowDomainException(string reservedVariant)
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Role.Create(Guid.NewGuid(), reservedVariant, "Intento reservado", ["Sales.View"], _timeProvider));

        Assert.Contains("reservado", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Vendedor Nocturno")]
    [InlineData("Cajero Auxiliar")]
    [InlineData("Supervisor de Turno B")]
    public void Create_WithCustomNonReservedName_ShouldSucceed(string customName)
    {
        // Act
        var role = Role.Create(Guid.NewGuid(), customName, "Rol personalizado", ["Sales.View"], _timeProvider);

        // Assert
        Assert.Equal(customName, role.Name);
    }

    [Theory]
    [InlineData(TenantRoles.TenantAdmin, TenantRoles.TenantAdmin)]
    [InlineData("tenantadmin", TenantRoles.TenantAdmin)]
    [InlineData(" Supervisor ", TenantRoles.Supervisor)]
    [InlineData(TenantRoles.InventoryManager, TenantRoles.InventoryManager)]
    [InlineData("cajero", TenantRoles.Cajero)]
    public void CreateSystemRole_WithCanonicalTenantRole_ShouldSucceedAndNormalizeName(string inputName, string expectedCanonicalName)
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        var permissions = new[] { "Sales.Create", "Products.View" };

        // Act
        var role = Role.CreateSystemRole(tenantId, inputName, "Rol de sistema", permissions, _timeProvider);

        // Assert
        Assert.Equal(tenantId, role.TenantId);
        Assert.Equal(expectedCanonicalName, role.Name);
        Assert.Equal(2, role.Permissions.Count);
    }

    [Theory]
    [InlineData(PlatformRoles.SuperAdmin)]
    [InlineData(" superadmin ")]
    [InlineData("CustomRole")]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateSystemRole_WithNonTenantSystemRole_ShouldThrowDomainException(string invalidName)
    {
        // Act & Assert
        Assert.Throws<DomainException>(() =>
            Role.CreateSystemRole(Guid.NewGuid(), invalidName, "Inválido", null, _timeProvider));
    }

    [Fact]
    public void CreateSystemRole_WithEmptyTenantId_ShouldThrowDomainException()
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Role.CreateSystemRole(PlatformScope.TenantId, TenantRoles.Cajero, "Sin tenant", null, _timeProvider));

        Assert.Contains("tenant", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
