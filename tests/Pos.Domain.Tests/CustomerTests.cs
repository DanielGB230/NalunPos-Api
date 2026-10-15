using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class CustomerTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateCustomerWithTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var taxId = TaxId.Create("20123456789", "PE");
        var address = Address.Create("Av. Principal 123", "Lima", "15001", "PE");

        // Act
        var customer = Customer.Create(
            tenantId,
            "Juan Pérez",
            taxId,
            "juan@test.com",
            "+51987654321",
            address);

        // Assert
        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.Equal(tenantId, customer.TenantId);
        Assert.Equal("Juan Pérez", customer.FullName);
        Assert.Equal(taxId, customer.TaxId);
        Assert.Equal("juan@test.com", customer.Email);
        Assert.Equal("+51987654321", customer.Phone);
        Assert.Equal(address, customer.Address);
        Assert.True(customer.IsActive);
    }

    [Fact]
    public void Create_WithPlatformScopeTenantId_ShouldThrowDomainException()
    {
        // Arrange
        var taxId = TaxId.Create("20123456789", "PE");

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Customer.Create(PlatformScope.TenantId, "Juan Pérez", taxId));

        Assert.Equal("El ID del tenant es requerido para crear un cliente.", ex.Message);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrowDomainException()
    {
        // Arrange
        var taxId = TaxId.Create("20123456789", "PE");

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Customer.Create(Guid.Empty, "Juan Pérez", taxId));

        Assert.Equal("El ID del tenant es requerido para crear un cliente.", ex.Message);
    }
}
