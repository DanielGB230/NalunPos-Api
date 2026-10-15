using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Xunit;

namespace Pos.Domain.Tests;

public class CashRegisterTests
{
    [Fact]
    public void CreateCashRegisterWithValidParametersShouldInstantiateActiveCashRegister()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        string name = "Caja Principal";
        string serialNumber = "CR-001";

        // Act
        var register = CashRegister.Create(tenantId, branchId, name, serialNumber);

        // Assert
        Assert.NotEqual(Guid.Empty, register.Id);
        Assert.Equal(tenantId, register.TenantId);
        Assert.Equal(branchId, register.BranchId);
        Assert.Equal(name, register.Name);
        Assert.Equal(serialNumber, register.SerialNumber);
        Assert.True(register.IsActive);
        Assert.Null(register.CurrentSessionId);
    }

    [Fact]
    public void CreateCashRegisterWithEmptyNameShouldThrowDomainException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            CashRegister.Create(tenantId, branchId, ""));
    }

    [Fact]
    public void CreateCashRegisterWithPlatformScopeTenantIdShouldThrowDomainException()
    {
        // Arrange
        var branchId = Guid.NewGuid();

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            CashRegister.Create(PlatformScope.TenantId, branchId, "Caja Test"));

        Assert.Equal("El ID del tenant es requerido para crear una caja.", ex.Message);
    }

    [Fact]
    public void CreateCashRegisterWithEmptyTenantIdShouldThrowDomainException()
    {
        // Arrange
        var branchId = Guid.NewGuid();

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            CashRegister.Create(Guid.Empty, branchId, "Caja Test"));

        Assert.Equal("El ID del tenant es requerido para crear una caja.", ex.Message);
    }

    [Fact]
    public void CreateCashRegisterWithEmptyBranchIdShouldThrowDomainException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            CashRegister.Create(tenantId, Guid.Empty, "Caja Test"));

        Assert.Equal("El ID de la sucursal es requerido para crear una caja.", ex.Message);
    }
}
