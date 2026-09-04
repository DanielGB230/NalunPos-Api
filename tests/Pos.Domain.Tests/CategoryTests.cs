using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Xunit;

namespace Pos.Domain.Tests;

public class CategoryTests
{
    [Fact]
    public void Create_WithValidName_ShouldInstantiateCategory()
    {
        // Arrange & Act
        var category = Category.Create("Periféricos", "Teclados y Mouses");

        // Assert
        Assert.NotEqual(Guid.Empty, category.Id);
        Assert.Equal("Periféricos", category.Name);
        Assert.Equal("Teclados y Mouses", category.Description);
        Assert.True(category.IsActive);
        Assert.Single(category.DomainEvents);
    }

    [Fact]
    public void Update_WithInvalidName_ShouldThrowDomainException()
    {
        // Arrange
        var category = Category.Create("Hardware");

        // Act & Assert
        Assert.Throws<DomainException>(() => category.Update("", "Descripción"));
    }
}
