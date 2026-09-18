using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class PurchaseOrderTests
{
    [Fact]
    public void CreatePurchaseOrderWithValidItemsShouldInstantiateDraftOrder()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Guid supplierId = Guid.NewGuid();
        Guid warehouseId = Guid.NewGuid();
        string orderNumber = "PO-2026-0001";
        var lines = new List<(Guid ProductId, decimal QuantityOrdered, Money UnitCost)>
        {
            (Guid.NewGuid(), 5m, Money.Create(150m, "USD"))
        };

        // Act
        var order = PurchaseOrder.Create(tenantId, supplierId, warehouseId, orderNumber, lines);

        // Assert
        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal(supplierId, order.SupplierId);
        Assert.Equal(warehouseId, order.WarehouseId);
        Assert.Equal(orderNumber, order.OrderNumber);
        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
        Assert.Single(order.Lines);
        Assert.Equal(750m, order.TotalOrderedCost.Amount);
    }

    [Fact]
    public void SendOrderShouldTransitionFromDraftToSent()
    {
        // Arrange
        var lines = new List<(Guid ProductId, decimal QuantityOrdered, Money UnitCost)>
        {
            (Guid.NewGuid(), 10m, Money.Create(40m, "USD"))
        };
        var order = PurchaseOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-002", lines);

        // Act
        order.Send();

        // Assert
        Assert.Equal(PurchaseOrderStatus.Sent, order.Status);
    }

    [Fact]
    public void ReceiveLinesShouldUpdateQuantityReceivedAndTransitionStatus()
    {
        // Arrange
        Guid productId = Guid.NewGuid();
        var lines = new List<(Guid ProductId, decimal QuantityOrdered, Money UnitCost)>
        {
            (productId, 10m, Money.Create(20m, "USD"))
        };
        var order = PurchaseOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-003", lines);
        order.Send();

        // Act
        var receivedLines = order.ReceiveLines(new[] { (productId, 10m) });

        // Assert
        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        Assert.Single(receivedLines);
        Assert.Equal(10m, receivedLines[0].QuantityReceived);
    }

    [Fact]
    public void ReceiveMoreThanOrderedShouldThrowDomainException()
    {
        // Arrange
        Guid productId = Guid.NewGuid();
        var lines = new List<(Guid ProductId, decimal QuantityOrdered, Money UnitCost)>
        {
            (productId, 5m, Money.Create(10m, "USD"))
        };
        var order = PurchaseOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-004", lines);
        order.Send();

        // Act & Assert
        Assert.Throws<DomainException>(() => order.ReceiveLines(new[] { (productId, 10m) }));
    }
}
