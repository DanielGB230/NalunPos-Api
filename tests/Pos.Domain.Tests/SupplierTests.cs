using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class SupplierTests
{
    [Fact]
    public void CreateWithValidParametersShouldInstantiateSupplierAndEmitEvent()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        string name = "Distribuidora Tech SAC";
        var taxId = TaxId.Create("20123456789", "PE");
        var address = Address.Create("Av. Principal 123", "Lima", "15001", "Perú");

        // Act
        var supplier = Supplier.Create(tenantId, name, taxId, address, "Juan Pérez", "juan@tech.com", "999888777");

        // Assert
        Assert.NotEqual(Guid.Empty, supplier.Id);
        Assert.Equal(tenantId, supplier.TenantId);
        Assert.Equal(name, supplier.Name);
        Assert.Equal(taxId, supplier.TaxId);
        Assert.Equal(address, supplier.Address);
        Assert.True(supplier.IsActive);
        Assert.Single(supplier.DomainEvents);
    }

    [Fact]
    public void Create_WithPlatformScopeTenantId_ShouldThrowDomainException()
    {
        // Arrange
        var taxId = TaxId.Create("20123456789", "PE");
        var address = Address.Create("Calle 1", "Lima", "15001", "Perú");

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Supplier.Create(PlatformScope.TenantId, "Proveedor", taxId, address, "Contacto", "test@test.com", "123"));

        Assert.Equal("El ID del tenant es requerido para crear un proveedor.", ex.Message);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrowDomainException()
    {
        // Arrange
        var taxId = TaxId.Create("20123456789", "PE");
        var address = Address.Create("Calle 1", "Lima", "15001", "Perú");

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Supplier.Create(Guid.Empty, "Proveedor", taxId, address, "Contacto", "test@test.com", "123"));

        Assert.Equal("El ID del tenant es requerido para crear un proveedor.", ex.Message);
    }

    [Fact]
    public void CreateWithEmptyNameShouldThrowDomainException()
    {
        // Arrange
        var taxId = TaxId.Create("20123456789", "PE");
        var address = Address.Create("Calle 1", "Lima", "15001", "Perú");

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            Supplier.Create(Guid.NewGuid(), "", taxId, address, "Contacto", "test@test.com", "123"));
    }

    [Fact]
    public void CreateAndUpdateSupplier_WithDeterministicTimeProvider_ShouldSetExactTimestampsAndEmitEvents()
    {
        // Arrange
        var createdTime = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
        var updatedTime = new DateTimeOffset(2026, 10, 21, 10, 30, 0, TimeSpan.Zero);
        var deactivatedTime = new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero);
        var timeProvider = new Common.FakeTimeProvider(createdTime);

        var tenantId = Guid.NewGuid();
        var taxId = TaxId.Create("20123456789", "PE");
        var address = Address.Create("Av. Principal 123", "Lima", "15001", "Perú");

        // Act - Create
        var supplier = Supplier.Create(tenantId, "Distribuidora Tech SAC", taxId, address, "Juan Pérez", "juan@tech.com", "999888777", timeProvider);

        // Assert - Create
        Assert.Equal(createdTime.UtcDateTime, supplier.CreatedAtUtc);
        Assert.Null(supplier.UpdatedAtUtc);
        var createdEvent = Assert.IsType<DomainEvents.SupplierCreatedDomainEvent>(Assert.Single(supplier.DomainEvents));
        Assert.Equal(createdTime.UtcDateTime, createdEvent.OccurredOnUtc);

        // Act - UpdateDetails
        timeProvider.SetUtcNow(updatedTime);
        supplier.UpdateDetails("Distribuidora Tech Global SAC", taxId, address, "Juan Pérez M.", "juan.perez@tech.com", "999111222", timeProvider);

        // Assert - UpdateDetails
        Assert.Equal(updatedTime.UtcDateTime, supplier.UpdatedAtUtc);
        var updatedEvent = supplier.DomainEvents.OfType<DomainEvents.SupplierUpdatedDomainEvent>().Single();
        Assert.Equal(updatedTime.UtcDateTime, updatedEvent.OccurredOnUtc);

        // Act - Deactivate
        timeProvider.SetUtcNow(deactivatedTime);
        supplier.Deactivate(timeProvider);

        // Assert - Deactivate
        Assert.False(supplier.IsActive);
        Assert.Equal(deactivatedTime.UtcDateTime, supplier.UpdatedAtUtc);
        var deactivatedEvent = supplier.DomainEvents.OfType<DomainEvents.SupplierDeactivatedDomainEvent>().Single();
        Assert.Equal(deactivatedTime.UtcDateTime, deactivatedEvent.OccurredOnUtc);
    }
}
