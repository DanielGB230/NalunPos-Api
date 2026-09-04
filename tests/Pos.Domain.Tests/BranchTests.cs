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
        string name = "Sucursal Central Lima";
        var address = Address.Create("Av. Javier Prado 1234", "Lima", "15034", "PE");
        string phone = "+51 987654321";

        // Act
        var branch = Branch.Create(name, address, phone);

        // Assert
        Assert.NotEqual(Guid.Empty, branch.Id);
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
        var address = Address.Create("Calle 1", "Ciudad", "001", "PE");

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            Branch.Create("", address));
    }
}
