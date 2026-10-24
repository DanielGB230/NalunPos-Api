using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class ProductTests
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    [Fact]
    public void Create_WithValidParameters_ShouldInstantiateProductAndRaiseDomainEvent()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        string name = "Teclado Mecánico RGB";
        var sku = Sku.Create("KEY-MECH-01");
        var price = Money.Create(129.99m, "USD");
        Guid categoryId = Guid.NewGuid();

        // Act
        var product = Product.Create(tenantId, name, sku, price, categoryId, _timeProvider);

        // Assert
        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.Equal(tenantId, product.TenantId);
        Assert.Equal(name, product.Name);
        Assert.Equal(sku, product.Sku);
        Assert.Equal(price, product.Price);
        Assert.Equal(categoryId, product.CategoryId);
        Assert.True(product.IsActive);
        Assert.Single(product.DomainEvents);
    }

    [Fact]
    public void Create_WithPlatformTenantId_ShouldThrowDomainException()
    {
        // Arrange
        var ex = Assert.Throws<DomainException>(() =>
            Product.Create(Pos.Domain.Common.PlatformScope.TenantId, "Mouse Gamer", Sku.Create("MSE-01"), Money.Create(50m, "USD"), Guid.NewGuid(), _timeProvider));

        Assert.Equal("El ID del tenant es requerido para crear un producto.", ex.Message);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrowDomainException()
    {
        // Arrange
        var ex = Assert.Throws<DomainException>(() =>
            Product.Create(Guid.Empty, "Mouse Gamer", Sku.Create("MSE-01"), Money.Create(50m, "USD"), Guid.NewGuid(), _timeProvider));

        Assert.Equal("El ID del tenant es requerido para crear un producto.", ex.Message);
    }

    [Fact]
    public void UpdatePrice_WithNewValidPrice_ShouldUpdatePriceAndEmitEvent()
    {
        // Arrange
        var product = Product.Create(Guid.NewGuid(), "Mouse Gamer", Sku.Create("MSE-01"), Money.Create(50m, "USD"), Guid.NewGuid(), _timeProvider);
        var newPrice = Money.Create(45m, "USD");

        // Act
        product.UpdatePrice(newPrice, _timeProvider);

        // Assert
        Assert.Equal(newPrice, product.Price);
        Assert.Contains(product.DomainEvents, e => e.GetType().Name == "ProductPriceUpdatedDomainEvent");
    }

    [Fact]
    public void Create_WithEmptyName_ShouldThrowDomainException()
    {
        // Arrange & Act & Assert
        Assert.Throws<DomainException>(() =>
            Product.Create(Guid.NewGuid(), "", Sku.Create("SKU-123"), Money.Create(10m, "USD"), Guid.NewGuid(), _timeProvider));
    }

    [Fact]
    public void Activate_WhenNotActive_ShouldActivateAndEmitProductStatusChangedDomainEvent()
    {
        // Arrange
        var product = Product.Create(Guid.NewGuid(), "Mouse Gamer", Sku.Create("MSE-01"), Money.Create(50m, "USD"), Guid.NewGuid(), _timeProvider);
        product.Deactivate(_timeProvider); // Set to inactive first
        product.ClearDomainEvents();

        // Act
        product.Activate(_timeProvider);

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
        var product = Product.Create(Guid.NewGuid(), "Mouse Gamer", Sku.Create("MSE-01"), Money.Create(50m, "USD"), Guid.NewGuid(), _timeProvider);
        product.ClearDomainEvents();

        // Act
        product.Activate(_timeProvider);

        // Assert
        Assert.True(product.IsActive);
        Assert.Empty(product.DomainEvents.OfType<DomainEvents.ProductStatusChangedDomainEvent>());
    }

    [Fact]
    public void Deactivate_WhenActive_ShouldDeactivateAndEmitProductStatusChangedDomainEvent()
    {
        // Arrange
        var product = Product.Create(Guid.NewGuid(), "Mouse Gamer", Sku.Create("MSE-01"), Money.Create(50m, "USD"), Guid.NewGuid(), _timeProvider);
        product.ClearDomainEvents();

        // Act
        product.Deactivate(_timeProvider);

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
        var product = Product.Create(Guid.NewGuid(), "Mouse Gamer", Sku.Create("MSE-01"), Money.Create(50m, "USD"), Guid.NewGuid(), _timeProvider);
        product.Deactivate(_timeProvider);
        product.ClearDomainEvents();

        // Act
        product.Deactivate(_timeProvider);

        // Assert
        Assert.False(product.IsActive);
        Assert.Empty(product.DomainEvents.OfType<DomainEvents.ProductStatusChangedDomainEvent>());
    }

    [Fact]
    public void CreateAndUpdateProduct_WithDeterministicTimeProvider_ShouldSetExactTimestampsAndEmitEvents()
    {
        // Arrange
        var createdTime = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        var updatedTime = new DateTimeOffset(2026, 10, 20, 12, 30, 0, TimeSpan.Zero);
        var priceUpdatedTime = new DateTimeOffset(2026, 10, 20, 13, 0, 0, TimeSpan.Zero);
        var deactivatedTime = new DateTimeOffset(2026, 10, 20, 13, 30, 0, TimeSpan.Zero);
        var timeProvider = new Common.FakeTimeProvider(createdTime);

        var tenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var sku = Sku.Create("MON-4K-01");
        var initialPrice = Money.Create(499.99m, "USD");

        // Act - Create
        var product = Product.Create(tenantId, "Monitor 4K", sku, initialPrice, categoryId, timeProvider);

        // Assert - Create
        Assert.Equal(createdTime.UtcDateTime, product.CreatedAtUtc);
        var createdEvent = Assert.IsType<DomainEvents.ProductCreatedDomainEvent>(Assert.Single(product.DomainEvents));
        Assert.Equal(createdTime.UtcDateTime, createdEvent.OccurredOnUtc);

        // Act - UpdateDetails
        timeProvider.SetUtcNow(updatedTime);
        product.UpdateDetails("Monitor 4K UHD Pro", "Pantalla IPS 144Hz", null, timeProvider);

        // Assert - UpdateDetails
        Assert.Equal(updatedTime.UtcDateTime, product.UpdatedAtUtc);
        var updatedEvent = product.DomainEvents.OfType<DomainEvents.ProductUpdatedDomainEvent>().Single();
        Assert.Equal(updatedTime.UtcDateTime, updatedEvent.OccurredOnUtc);

        // Act - UpdatePrice
        timeProvider.SetUtcNow(priceUpdatedTime);
        product.UpdatePrice(Money.Create(449.99m, "USD"), timeProvider);

        // Assert - UpdatePrice
        Assert.Equal(priceUpdatedTime.UtcDateTime, product.UpdatedAtUtc);
        var priceEvent = product.DomainEvents.OfType<DomainEvents.ProductPriceUpdatedDomainEvent>().Single();
        Assert.Equal(priceUpdatedTime.UtcDateTime, priceEvent.OccurredOnUtc);

        // Act - Deactivate
        timeProvider.SetUtcNow(deactivatedTime);
        product.Deactivate(timeProvider);

        // Assert - Deactivate
        Assert.Equal(deactivatedTime.UtcDateTime, product.UpdatedAtUtc);
        var statusEvent = product.DomainEvents.OfType<DomainEvents.ProductStatusChangedDomainEvent>().Single();
        Assert.Equal(deactivatedTime.UtcDateTime, statusEvent.OccurredOnUtc);
    }
}
