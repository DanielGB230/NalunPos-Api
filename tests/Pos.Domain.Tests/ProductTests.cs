using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class ProductTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldInstantiateProductAndRaiseDomainEvent()
    {
        // Arrange
        string name = "Teclado Mecánico RGB";
        var sku = Sku.Create("KEY-MECH-01");
        var price = Money.Create(129.99m, "USD");
        Guid categoryId = Guid.NewGuid();

        // Act
        var product = Product.Create(name, sku, price, categoryId);

        // Assert
        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.Equal(name, product.Name);
        Assert.Equal(sku, product.Sku);
        Assert.Equal(price, product.Price);
        Assert.Equal(categoryId, product.CategoryId);
        Assert.True(product.IsActive);
        Assert.Single(product.DomainEvents);
    }

    [Fact]
    public void UpdatePrice_WithNewValidPrice_ShouldUpdatePriceAndEmitEvent()
    {
        // Arrange
        var product = Product.Create("Mouse Gamer", Sku.Create("MSE-01"), Money.Create(50m, "USD"), Guid.NewGuid());
        var newPrice = Money.Create(45m, "USD");

        // Act
        product.UpdatePrice(newPrice);

        // Assert
        Assert.Equal(newPrice, product.Price);
        Assert.Contains(product.DomainEvents, e => e.GetType().Name == "ProductPriceUpdatedDomainEvent");
    }

    [Fact]
    public void Create_WithEmptyName_ShouldThrowDomainException()
    {
        // Arrange & Act & Assert
        Assert.Throws<DomainException>(() =>
            Product.Create("", Sku.Create("SKU-123"), Money.Create(10m, "USD"), Guid.NewGuid()));
    }

    [Fact]
    public void Activate_WhenNotActive_ShouldActivateAndEmitProductStatusChangedDomainEvent()
    {
        // Arrange
        var product = Product.Create("Mouse Gamer", Sku.Create("MSE-01"), Money.Create(50m, "USD"), Guid.NewGuid());
        product.Deactivate(); // Set to inactive first
        product.ClearDomainEvents();

        // Act
        product.Activate();

        // Assert
        Assert.True(product.IsActive);
        var statusEvent = product.DomainEvents.OfType<DomainEvents.ProductStatusChangedDomainEvent>().SingleOrDefault();
        Assert.NotNull(statusEvent);
        Assert.Equal(product.Id, statusEvent.ProductId);
        Assert.Equal(product.TenantId, statusEvent.TenantId);
        Assert.True(statusEvent.IsActive);
        Assert.True(statusEvent.OccurredOnUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ShouldNotEmitEvent()
    {
        // Arrange
        var product = Product.Create("Mouse Gamer", Sku.Create("MSE-01"), Money.Create(50m, "USD"), Guid.NewGuid());
        product.ClearDomainEvents();

        // Act
        product.Activate();

        // Assert
        Assert.True(product.IsActive);
        Assert.Empty(product.DomainEvents.OfType<DomainEvents.ProductStatusChangedDomainEvent>());
    }

    [Fact]
    public void Deactivate_WhenActive_ShouldDeactivateAndEmitProductStatusChangedDomainEvent()
    {
        // Arrange
        var product = Product.Create("Mouse Gamer", Sku.Create("MSE-01"), Money.Create(50m, "USD"), Guid.NewGuid());
        product.ClearDomainEvents();

        // Act
        product.Deactivate();

        // Assert
        Assert.False(product.IsActive);
        var statusEvent = product.DomainEvents.OfType<DomainEvents.ProductStatusChangedDomainEvent>().SingleOrDefault();
        Assert.NotNull(statusEvent);
        Assert.Equal(product.Id, statusEvent.ProductId);
        Assert.Equal(product.TenantId, statusEvent.TenantId);
        Assert.False(statusEvent.IsActive);
        Assert.True(statusEvent.OccurredOnUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldNotEmitEvent()
    {
        // Arrange
        var product = Product.Create("Mouse Gamer", Sku.Create("MSE-01"), Money.Create(50m, "USD"), Guid.NewGuid());
        product.Deactivate();
        product.ClearDomainEvents();

        // Act
        product.Deactivate();

        // Assert
        Assert.False(product.IsActive);
        Assert.Empty(product.DomainEvents.OfType<DomainEvents.ProductStatusChangedDomainEvent>());
    }
}
