using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class SaleTests
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    [Fact]
    public void CreateSaleWithValidItemsShouldCalculateTotalsAndEmitSaleCompletedDomainEvent()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        string receiptNumber = "REC-2026-0001";
        Guid sessionId = Guid.NewGuid();
        Guid productId1 = Guid.NewGuid();
        Guid productId2 = Guid.NewGuid();

        var item1 = SaleLineItem.Create(tenantId, productId1, "Teclado Mecánico", 2m, Money.Create(100m, "USD"));
        var item2 = SaleLineItem.Create(tenantId, productId2, "Mouse Gamer", 1m, Money.Create(50m, "USD"));
        var items = new List<SaleLineItem> { item1, item2 };

        // Act
        var sale = Sale.Create(tenantId, receiptNumber, sessionId, null, items, 18m, "USD", _timeProvider);

        // Assert
        Assert.NotEqual(Guid.Empty, sale.Id);
        Assert.Equal(tenantId, sale.TenantId);
        Assert.Equal(receiptNumber, sale.ReceiptNumber);
        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Equal(250m, sale.SubTotal.Amount); // (2*100) + (1*50) = 250
        Assert.Equal(45m, sale.TaxAmount.Amount); // 250 * 0.18 = 45
        Assert.Equal(295m, sale.Total.Amount); // 250 + 45 = 295
        Assert.Equal(2, sale.LineItems.Count);

        Assert.Single(sale.DomainEvents);
        var domainEvent = sale.DomainEvents.First();
        Assert.Equal("SaleCompletedDomainEvent", domainEvent.GetType().Name);
    }

    [Fact]
    public void CreateSale_WithItemsFromDifferentTenant_ShouldThrowDomainException()
    {
        // Arrange
        Guid saleTenantId = Guid.NewGuid();
        Guid otherTenantId = Guid.NewGuid();
        var itemOfOtherTenant = SaleLineItem.Create(otherTenantId, Guid.NewGuid(), "Producto Foráneo", 1m, Money.Create(50m, "USD"));

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Sale.Create(saleTenantId, "REC-001", Guid.NewGuid(), null, new[] { itemOfOtherTenant }, _timeProvider));

        Assert.Equal("La venta y sus líneas no pueden pertenecer a tenants distintos.", ex.Message);
    }

    [Fact]
    public void CreateSale_WithPlatformScopeTenantId_ShouldThrowDomainException()
    {
        // Arrange
        var item = SaleLineItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Producto", 1m, Money.Create(50m, "USD"));

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Sale.Create(PlatformScope.TenantId, "REC-001", Guid.NewGuid(), null, new[] { item }, _timeProvider));

        Assert.Equal("El ID del tenant es requerido para crear una venta.", ex.Message);
    }

    [Fact]
    public void CreateSaleLineItem_WithPlatformScopeTenantId_ShouldThrowDomainException()
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            SaleLineItem.Create(PlatformScope.TenantId, Guid.NewGuid(), "Producto", 1m, Money.Create(50m, "USD")));

        Assert.Equal("El ID del tenant es requerido para crear una línea de venta.", ex.Message);
    }

    [Fact]
    public void CreateSaleLineItem_WithEmptyTenantId_ShouldThrowDomainException()
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            SaleLineItem.Create(Guid.Empty, Guid.NewGuid(), "Producto", 1m, Money.Create(50m, "USD")));

        Assert.Equal("El ID del tenant es requerido para crear una línea de venta.", ex.Message);
    }

    [Fact]
    public void CreateSaleWithNoItemsShouldThrowDomainException()
    {
        // Arrange & Act & Assert
        Assert.Throws<DomainException>(() =>
            Sale.Create(Guid.NewGuid(), "REC-001", Guid.NewGuid(), null, new List<SaleLineItem>(), _timeProvider));
    }

    [Fact]
    public void CreateAndCancelSale_WithDeterministicTimeProvider_ShouldSetExactTimestampsAndEmitEvents()
    {
        // Arrange
        var createdTime = new DateTimeOffset(2026, 10, 20, 14, 0, 0, TimeSpan.Zero);
        var cancelledTime = new DateTimeOffset(2026, 10, 20, 14, 15, 0, TimeSpan.Zero);
        var timeProvider = new Common.FakeTimeProvider(createdTime);

        var tenantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var item = SaleLineItem.Create(tenantId, productId, "Producto Test", 1m, Money.Create(100m, "USD"));

        // Act - Create
        var sale = Sale.Create(tenantId, "REC-DET-001", sessionId, null, new[] { item }, 0m, "USD", timeProvider);

        // Assert - Create
        Assert.Equal(createdTime.UtcDateTime, sale.CreatedAtUtc);
        var completedEvent = Assert.IsType<DomainEvents.SaleCompletedDomainEvent>(Assert.Single(sale.DomainEvents));
        Assert.Equal(createdTime.UtcDateTime, completedEvent.OccurredOnUtc);

        // Act - Cancel
        timeProvider.SetUtcNow(cancelledTime);
        sale.Cancel(timeProvider);

        // Assert - Cancel
        Assert.Equal(SaleStatus.Cancelled, sale.Status);
        var cancelledEvent = sale.DomainEvents.OfType<DomainEvents.SaleCancelledDomainEvent>().Single();
        Assert.Equal(cancelledTime.UtcDateTime, cancelledEvent.OccurredOnUtc);
    }
}
