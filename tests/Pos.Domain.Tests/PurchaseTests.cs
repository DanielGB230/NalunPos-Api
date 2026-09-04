using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class PurchaseTests
{
    [Fact]
    public void CreatePurchaseWithValidItemsShouldInstantiateDraftPurchase()
    {
        // Arrange
        Guid supplierId = Guid.NewGuid();
        string orderNumber = "PO-2026-0001";
        var item1 = PurchaseLineItem.Create(Guid.NewGuid(), "Impresora Térmica POS", 5m, Money.Create(150m, "USD"));
        var items = new List<PurchaseLineItem> { item1 };

        // Act
        var purchase = Purchase.Create(supplierId, orderNumber, items, "USD");

        // Assert
        Assert.NotEqual(Guid.Empty, purchase.Id);
        Assert.Equal(supplierId, purchase.SupplierId);
        Assert.Equal(orderNumber, purchase.OrderNumber);
        Assert.Equal(PurchaseStatus.Draft, purchase.Status);
        Assert.Equal(750m, purchase.TotalAmount.Amount);
    }

    [Fact]
    public void CompletePurchaseShouldChangeStatusAndEmitPurchaseCompletedDomainEvent()
    {
        // Arrange
        var item = PurchaseLineItem.Create(Guid.NewGuid(), "Lector Código Barras", 10m, Money.Create(40m, "USD"));
        var purchase = Purchase.Create(Guid.NewGuid(), "PO-002", new List<PurchaseLineItem> { item }, "USD");

        // Act
        purchase.Complete();

        // Assert
        Assert.Equal(PurchaseStatus.Completed, purchase.Status);
        Assert.Single(purchase.DomainEvents);
        var domainEvent = purchase.DomainEvents.First();
        Assert.Equal("PurchaseCompletedDomainEvent", domainEvent.GetType().Name);
    }
}
