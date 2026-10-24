using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Xunit;

namespace Pos.Domain.Tests;

public class WarehouseTests
{
    [Fact]
    public void CreateWarehouse_WithValidParameters_ShouldInstantiateWarehouseAndEmitEvent()
    {
        // Arrange
        var createdTime = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
        var timeProvider = new Common.FakeTimeProvider(createdTime);

        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        // Act
        var warehouse = Warehouse.Create(tenantId, branchId, "Almacén Principal", "Depósito central de mercadería", isDefault: true, timeProvider);

        // Assert
        Assert.NotEqual(Guid.Empty, warehouse.Id);
        Assert.Equal(tenantId, warehouse.TenantId);
        Assert.Equal(branchId, warehouse.BranchId);
        Assert.Equal("Almacén Principal", warehouse.Name);
        Assert.Equal("Depósito central de mercadería", warehouse.Description);
        Assert.True(warehouse.IsDefault);
        Assert.True(warehouse.IsActive);
        Assert.Equal(createdTime.UtcDateTime, warehouse.CreatedAtUtc);
        Assert.Null(warehouse.UpdatedAtUtc);

        var createdEvent = Assert.IsType<DomainEvents.WarehouseCreatedDomainEvent>(Assert.Single(warehouse.DomainEvents));
        Assert.Equal(createdTime.UtcDateTime, createdEvent.OccurredOnUtc);
    }

    [Fact]
    public void UpdateDetailsAndSetDefault_WithDeterministicTimeProvider_ShouldSetExactTimestamps()
    {
        // Arrange
        var createdTime = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
        var updatedTime = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
        var defaultTime = new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero);
        var timeProvider = new Common.FakeTimeProvider(createdTime);

        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var warehouse = Warehouse.Create(tenantId, branchId, "Almacén Secundario", isDefault: false, timeProvider: timeProvider);

        // Act - UpdateDetails
        timeProvider.SetUtcNow(updatedTime);
        warehouse.UpdateDetails("Almacén Secundario Lima", "Ampliación de zona norte", timeProvider);

        // Assert - UpdateDetails
        Assert.Equal("Almacén Secundario Lima", warehouse.Name);
        Assert.Equal(updatedTime.UtcDateTime, warehouse.UpdatedAtUtc);

        // Act - SetAsDefault
        timeProvider.SetUtcNow(defaultTime);
        warehouse.SetAsDefault(timeProvider);

        // Assert - SetAsDefault
        Assert.True(warehouse.IsDefault);
        Assert.Equal(defaultTime.UtcDateTime, warehouse.UpdatedAtUtc);
    }

    [Fact]
    public void Deactivate_WhenWarehouseIsDefault_ShouldThrowDomainException()
    {
        // Arrange
        var timeProvider = new Common.FakeTimeProvider(DateTimeOffset.UtcNow);
        var warehouse = Warehouse.Create(Guid.NewGuid(), Guid.NewGuid(), "Almacén Default", isDefault: true, timeProvider);

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() => warehouse.Deactivate(timeProvider));
        Assert.Equal("No se puede desactivar el almacén predeterminado. Asigne otro almacén por defecto primero.", ex.Message);
    }

    [Fact]
    public void CreateWarehouse_WithEmptyTenantId_ShouldThrowDomainExceptionViaTenantGuard()
    {
        // Arrange
        var branchId = Guid.NewGuid();
        var timeProvider = new Common.FakeTimeProvider(DateTimeOffset.UtcNow);

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Warehouse.Create(Guid.Empty, branchId, "Almacén Sin Tenant", timeProvider));

        Assert.Equal("El ID del tenant es requerido para crear un almacén.", ex.Message);
    }

    [Fact]
    public void CreateWarehouse_WithPlatformScopeTenantId_ShouldThrowDomainExceptionViaTenantGuard()
    {
        // Arrange
        var branchId = Guid.NewGuid();
        var timeProvider = new Common.FakeTimeProvider(DateTimeOffset.UtcNow);

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Warehouse.Create(PlatformScope.TenantId, branchId, "Almacén Plataforma", timeProvider));

        Assert.Equal("El ID del tenant es requerido para crear un almacén.", ex.Message);
    }

    [Fact]
    public void CreateWarehouse_WithEmptyBranchId_ShouldThrowDomainException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var timeProvider = new Common.FakeTimeProvider(DateTimeOffset.UtcNow);

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Warehouse.Create(tenantId, Guid.Empty, "Almacén Sin Sucursal", timeProvider));

        Assert.Equal("El ID de la sucursal es requerido para crear un almacén.", ex.Message);
    }
}
