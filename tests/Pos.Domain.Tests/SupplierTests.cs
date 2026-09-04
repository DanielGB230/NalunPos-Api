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
        string name = "Distribuidora Tech SAC";
        var taxId = TaxId.Create("20123456789", "PE");
        var address = Address.Create("Av. Principal 123", "Lima", "15001", "Perú");

        // Act
        var supplier = Supplier.Create(name, taxId, address, "Juan Pérez", "juan@tech.com", "999888777");

        // Assert
        Assert.NotEqual(Guid.Empty, supplier.Id);
        Assert.Equal(name, supplier.Name);
        Assert.Equal(taxId, supplier.TaxId);
        Assert.Equal(address, supplier.Address);
        Assert.True(supplier.IsActive);
        Assert.Single(supplier.DomainEvents);
    }

    [Fact]
    public void CreateWithEmptyNameShouldThrowDomainException()
    {
        // Arrange
        var taxId = TaxId.Create("20123456789", "PE");
        var address = Address.Create("Calle 1", "Lima", "15001", "Perú");

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            Supplier.Create("", taxId, address, "Contacto", "test@test.com", "123"));
    }
}
