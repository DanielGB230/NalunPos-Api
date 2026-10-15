using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class BranchTests
{
    [Fact]
    public void CreateBranchWithValidParametersShouldInstantiateActiveBranch()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        string name = "Sucursal Central Lima";
        var address = Address.Create("Av. Javier Prado 1234", "Lima", "15034", "PE");
        string phone = "+51 987654321";

        // Act
        var branch = Branch.Create(tenantId, name, address, phone);

        // Assert
        Assert.NotEqual(Guid.Empty, branch.Id);
        Assert.Equal(tenantId, branch.TenantId);
        Assert.Equal(name, branch.Name);
        Assert.Equal(address, branch.Address);
        Assert.Equal(phone, branch.PhoneNumber);
        Assert.True(branch.IsActive);
        Assert.Single(branch.DomainEvents);
    }

    [Fact]
    public void CreateBranchWithEmptyNameShouldThrowDomainException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var address = Address.Create("Calle 1", "Ciudad", "001", "PE");

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            Branch.Create(tenantId, "", address));
    }

    [Fact]
    public void CreateBranchWithPlatformScopeTenantIdShouldThrowDomainException()
    {
        // Arrange
        var address = Address.Create("Calle 1", "Ciudad", "001", "PE");

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Branch.Create(PlatformScope.TenantId, "Sucursal Test", address));

        Assert.Equal("El ID del tenant es requerido para crear una sucursal.", ex.Message);
    }

    [Fact]
    public void CreateBranchWithEmptyTenantIdShouldThrowDomainException()
    {
        // Arrange
        var address = Address.Create("Calle 1", "Ciudad", "001", "PE");

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Branch.Create(Guid.Empty, "Sucursal Test", address));

        Assert.Equal("El ID del tenant es requerido para crear una sucursal.", ex.Message);
    }
}

