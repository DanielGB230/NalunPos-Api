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

    [Fact]
    public void CreatePlatformRole_WithCanonicalArguments_ShouldSucceed()
    {
        // Arrange
        var permissions = new[] { "Permissions.Platform.DeadLetter.View" };

        // Act
        var role = Role.CreatePlatformRole(
            Role.SuperAdminRoleId,
            PlatformScope.TenantId,
            PlatformRoles.SuperAdmin,
            "Administrador Global",
            permissions,
            _timeProvider);

        // Assert
        Assert.Equal(Role.SuperAdminRoleId, role.Id);
        Assert.Equal(PlatformScope.TenantId, role.TenantId);
        Assert.Equal(PlatformRoles.SuperAdmin, role.Name);
        Assert.Single(role.Permissions);
    }

    [Theory]
    [InlineData("SuperAdminRole")]
    [InlineData("superadmin")]
    [InlineData(" SuperAdmin ")]
    [InlineData(TenantRoles.TenantAdmin)]
    [InlineData("CustomRole")]
    [InlineData("")]
    [InlineData("   ")]
    public void CreatePlatformRole_WithNonCanonicalName_ShouldThrowDomainException(string invalidName)
    {
        // Act & Assert
        Assert.Throws<DomainException>(() =>
            Role.CreatePlatformRole(
                Role.SuperAdminRoleId,
                PlatformScope.TenantId,
                invalidName,
                "Inválido",
                null,
                _timeProvider));
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("00000000-0000-0000-0000-000000000002")]
    public void CreatePlatformRole_WithNonCanonicalId_ShouldThrowDomainException(string invalidIdText)
    {
        // Arrange
        Guid invalidId = Guid.Parse(invalidIdText);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            Role.CreatePlatformRole(
                invalidId,
                PlatformScope.TenantId,
                PlatformRoles.SuperAdmin,
                "ID inválido",
                null,
                _timeProvider));
    }

    [Fact]
    public void CreatePlatformRole_WithNonPlatformTenantId_ShouldThrowDomainException()
    {
        // Act & Assert
        Assert.Throws<DomainException>(() =>
            Role.CreatePlatformRole(
                Role.SuperAdminRoleId,
                Guid.NewGuid(),
                PlatformRoles.SuperAdmin,
                "Tenant inválido",
                null,
                _timeProvider));
    }

    private static readonly string[] s_initialPermissions = ["Permissions.Platform.DeadLetter.View", "Permissions.Platform.DeadLetter.Reprocess", "Permissions.Obsolete.Legacy"];
    private static readonly string[] s_targetPermissions =
    [
        "Permissions.Platform.DeadLetter.View",
        "Permissions.Platform.Tenants.View",
        "Permissions.Platform.Tenants.Create",
        "permissions.platform.tenants.create",
        "   "
    ];

    [Fact]
    public void SyncPermissions_AddsMissingRemovesObsoleteDeduplicatesAndIsIdempotent()
    {
        // Arrange
        var role = Role.CreatePlatformRole(
            Role.SuperAdminRoleId,
            PlatformScope.TenantId,
            PlatformRoles.SuperAdmin,
            "Administrador Global",
            s_initialPermissions,
            _timeProvider);

        // Act 1: Sincronizar permisos
        role.SyncPermissions(s_targetPermissions);

        // Assert 1: Debe contener exactamente los permisos target válidos sin duplicados ni obsoletos
        Assert.Equal(3, role.Permissions.Count);
        Assert.Contains("Permissions.Platform.DeadLetter.View", role.Permissions);
        Assert.Contains("Permissions.Platform.Tenants.View", role.Permissions);
        Assert.Contains("Permissions.Platform.Tenants.Create", role.Permissions);
        Assert.DoesNotContain("Permissions.Platform.DeadLetter.Reprocess", role.Permissions);
        Assert.DoesNotContain("Permissions.Obsolete.Legacy", role.Permissions);

        // Act 2: Idempotencia - sincronizar de nuevo no altera la colección
        role.SyncPermissions(s_targetPermissions);
        Assert.Equal(3, role.Permissions.Count);
    }

    [Fact]
    public void SyncPermissions_WithNullTargetPermissions_ThrowsArgumentNullException()
    {
        var role = Role.CreatePlatformRole(
            Role.SuperAdminRoleId,
            PlatformScope.TenantId,
            PlatformRoles.SuperAdmin,
            "Administrador Global",
            null,
            _timeProvider);

        Assert.Throws<ArgumentNullException>(() => role.SyncPermissions(null!));
    }
}
