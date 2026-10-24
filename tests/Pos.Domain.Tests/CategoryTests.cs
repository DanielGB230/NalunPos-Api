using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Xunit;

namespace Pos.Domain.Tests;

public class CategoryTests
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    [Fact]
    public void Create_WithValidName_ShouldInstantiateCategory()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var category = Category.Create(tenantId, "Periféricos", "Teclados y Mouses", _timeProvider);

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
            Category.Create(PlatformScope.TenantId, "Periféricos", "Descripción", _timeProvider));

        Assert.Equal("El ID del tenant es requerido para crear una categoría.", ex.Message);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrowDomainException()
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Category.Create(Guid.Empty, "Periféricos", "Descripción", _timeProvider));

        Assert.Equal("El ID del tenant es requerido para crear una categoría.", ex.Message);
    }

    [Fact]
    public void Update_WithInvalidName_ShouldThrowDomainException()
    {
        // Arrange
        var category = Category.Create(Guid.NewGuid(), "Hardware", _timeProvider);

        // Act & Assert
        Assert.Throws<DomainException>(() => category.Update("", "Descripción", _timeProvider));
    }

    [Fact]
    public void CreateAndUpdateCategory_WithDeterministicTimeProvider_ShouldSetExactTimestamps()
    {
        // Arrange
        var createdTime = new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);
        var updatedTime = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        var deactivatedTime = new DateTimeOffset(2026, 10, 20, 14, 0, 0, TimeSpan.Zero);
        var timeProvider = new Common.FakeTimeProvider(createdTime);

        var tenantId = Guid.NewGuid();

        // Act - Create
        var category = Category.Create(tenantId, "Bebidas", "Gaseosas y Jugos", timeProvider);

        // Assert - Create
        Assert.Equal(createdTime.UtcDateTime, category.CreatedAtUtc);
        Assert.Null(category.UpdatedAtUtc);

        // Act - Update
        timeProvider.SetUtcNow(updatedTime);
        category.Update("Bebidas y Refrescos", "Gaseosas, Aguas y Jugos", timeProvider);

        // Assert - Update
        Assert.Equal(updatedTime.UtcDateTime, category.UpdatedAtUtc);

        // Act - Deactivate
        timeProvider.SetUtcNow(deactivatedTime);
        category.Deactivate(timeProvider);

        // Assert - Deactivate
        Assert.False(category.IsActive);
        Assert.Equal(deactivatedTime.UtcDateTime, category.UpdatedAtUtc);
    }
}
