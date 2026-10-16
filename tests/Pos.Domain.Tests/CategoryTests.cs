using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Xunit;

namespace Pos.Domain.Tests;

public class CategoryTests
{
    [Fact]
    public void Create_WithValidName_ShouldInstantiateCategory()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var category = Category.Create(tenantId, "Periféricos", "Teclados y Mouses");

        // Assert
        Assert.NotEqual(Guid.Empty, category.Id);
        Assert.Equal(tenantId, category.TenantId);
        Assert.Equal("Periféricos", category.Name);
        Assert.Equal("Teclados y Mouses", category.Description);
        Assert.True(category.IsActive);
        Assert.Single(category.DomainEvents);
    }

    [Fact]
    public void Create_WithPlatformScopeTenantId_ShouldThrowDomainException()
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Category.Create(PlatformScope.TenantId, "Periféricos", "Descripción"));

        Assert.Equal("El ID del tenant es requerido para crear una categoría.", ex.Message);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrowDomainException()
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Category.Create(Guid.Empty, "Periféricos", "Descripción"));

        Assert.Equal("El ID del tenant es requerido para crear una categoría.", ex.Message);
    }

    [Fact]
    public void Update_WithInvalidName_ShouldThrowDomainException()
    {
        // Arrange
        var category = Category.Create(Guid.NewGuid(), "Hardware");

        // Act & Assert
        Assert.Throws<DomainException>(() => category.Update("", "Descripción"));
    }
}
