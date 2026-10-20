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

    [Fact]
    public void CreateAndUpdateBranch_WithDeterministicTimeProvider_ShouldSetExactTimestamps()
    {
        // Arrange
        var createdTime = new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
        var updatedTime = new DateTimeOffset(2026, 10, 20, 11, 0, 0, TimeSpan.Zero);
        var timeProvider = new Common.FakeTimeProvider(createdTime);

        var tenantId = Guid.NewGuid();
        var address = Address.Create("Av. Larco 456", "Lima", "15074", "PE");

        // Act - Create
        var branch = Branch.Create(tenantId, "Sucursal Miraflores", address, "+51 123456789", timeProvider);

        // Assert - Create
        Assert.Equal(createdTime.UtcDateTime, branch.CreatedAtUtc);
        Assert.Null(branch.UpdatedAtUtc);

        // Act - UpdateDetails
        timeProvider.SetUtcNow(updatedTime);
        var newAddress = Address.Create("Av. Benavides 789", "Lima", "15074", "PE");
        branch.UpdateDetails("Sucursal Miraflores Premium", newAddress, "+51 999888777", timeProvider);

        // Assert - Update
        Assert.Equal(updatedTime.UtcDateTime, branch.UpdatedAtUtc);
    }
}

