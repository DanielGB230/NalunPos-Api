using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class PurchaseOrderTests
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;

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
        var order = PurchaseOrder.Create(tenantId, supplierId, warehouseId, orderNumber, lines, _timeProvider);

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
    public void Send_FromDraft_ShouldTransitionToSentAndEmitPurchaseOrderSentDomainEvent()
    {
        // Arrange
        var lines = new List<(Guid ProductId, decimal QuantityOrdered, Money UnitCost)>
        {
            (Guid.NewGuid(), 10m, Money.Create(40m, "USD"))
        };
        var order = PurchaseOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-002", lines, _timeProvider);
        order.ClearDomainEvents();

        // Act
        order.Send(_timeProvider);

        // Assert
        Assert.Equal(PurchaseOrderStatus.Sent, order.Status);
        
        var sentEvent = order.DomainEvents.OfType<DomainEvents.PurchaseOrderSentDomainEvent>().SingleOrDefault();
        Assert.NotNull(sentEvent);
        Assert.Equal(order.Id, sentEvent.PurchaseOrderId);
        Assert.Equal(order.TenantId, sentEvent.TenantId);
        Assert.Equal(order.SupplierId, sentEvent.SupplierId);
        Assert.Equal(order.WarehouseId, sentEvent.WarehouseId);
        Assert.Equal(order.OrderNumber, sentEvent.OrderNumber);
        Assert.True(sentEvent.OccurredOnUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void Send_FromSent_ShouldThrowDomainExceptionAndNotEmitEvent()
    {
        // Arrange
        var lines = new List<(Guid ProductId, decimal QuantityOrdered, Money UnitCost)>
        {
            (Guid.NewGuid(), 10m, Money.Create(40m, "USD"))
        };
        var order = PurchaseOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-002", lines, _timeProvider);
        order.Send(_timeProvider);
        order.ClearDomainEvents();

        // Act & Assert
        Assert.Throws<DomainException>(() => order.Send(_timeProvider));
        Assert.Empty(order.DomainEvents.OfType<DomainEvents.PurchaseOrderSentDomainEvent>());
    }

    [Fact]
    public void Send_FromReceived_ShouldThrowDomainExceptionAndNotEmitEvent()
    {
        // Arrange
        Guid productId = Guid.NewGuid();
        var lines = new List<(Guid ProductId, decimal QuantityOrdered, Money UnitCost)>
        {
            (productId, 10m, Money.Create(40m, "USD"))
        };
        var order = PurchaseOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-002", lines, _timeProvider);
        order.Send(_timeProvider);
        order.ReceiveLines(new[] { (productId, 10m) }, _timeProvider);
        order.ClearDomainEvents();

        // Act & Assert
        Assert.Throws<DomainException>(() => order.Send(_timeProvider));
        Assert.Empty(order.DomainEvents.OfType<DomainEvents.PurchaseOrderSentDomainEvent>());
    }

    [Fact]
    public void Send_FromCancelled_ShouldThrowDomainExceptionAndNotEmitEvent()
    {
        // Arrange
        var lines = new List<(Guid ProductId, decimal QuantityOrdered, Money UnitCost)>
        {
            (Guid.NewGuid(), 10m, Money.Create(40m, "USD"))
        };
        var order = PurchaseOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-002", lines, _timeProvider);
        order.Cancel(_timeProvider);
        order.ClearDomainEvents();

        // Act & Assert
        Assert.Throws<DomainException>(() => order.Send(_timeProvider));
        Assert.Empty(order.DomainEvents.OfType<DomainEvents.PurchaseOrderSentDomainEvent>());
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
        var order = PurchaseOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-003", lines, _timeProvider);
        order.Send(_timeProvider);

        // Act
        var receivedLines = order.ReceiveLines(new[] { (productId, 10m) }, _timeProvider);

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
        var order = PurchaseOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-004", lines, _timeProvider);
        order.Send(_timeProvider);

        // Act & Assert
        Assert.Throws<DomainException>(() => order.ReceiveLines(new[] { (productId, 10m) }, _timeProvider));
    }

    [Fact]
    public void Cancel_FromDraft_ShouldTransitionToCancelledAndEmitDomainEvent()
    {
        // Arrange
        var lines = new List<(Guid ProductId, decimal QuantityOrdered, Money UnitCost)>
        {
            (Guid.NewGuid(), 10m, Money.Create(40m, "USD"))
        };
        var order = PurchaseOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-005", lines, _timeProvider);
        order.ClearDomainEvents();

        // Act
        order.Cancel(_timeProvider);

        // Assert
        Assert.Equal(PurchaseOrderStatus.Cancelled, order.Status);
        
        var cancelledEvent = order.DomainEvents.OfType<DomainEvents.PurchaseOrderCancelledDomainEvent>().SingleOrDefault();
        Assert.NotNull(cancelledEvent);
        Assert.Equal(order.Id, cancelledEvent.PurchaseOrderId);
        Assert.Equal(PurchaseOrderStatus.Draft, cancelledEvent.PreviousStatus);
    }

    [Fact]
    public void Cancel_FromSent_ShouldTransitionToCancelledAndEmitDomainEvent()
    {
        // Arrange
        var lines = new List<(Guid ProductId, decimal QuantityOrdered, Money UnitCost)>
        {
            (Guid.NewGuid(), 10m, Money.Create(40m, "USD"))
        };
        var order = PurchaseOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-006", lines, _timeProvider);
        order.Send(_timeProvider);
        order.ClearDomainEvents();

        // Act
        order.Cancel(_timeProvider);

        // Assert
        Assert.Equal(PurchaseOrderStatus.Cancelled, order.Status);
        
        var cancelledEvent = order.DomainEvents.OfType<DomainEvents.PurchaseOrderCancelledDomainEvent>().SingleOrDefault();
        Assert.NotNull(cancelledEvent);
        Assert.Equal(order.Id, cancelledEvent.PurchaseOrderId);
        Assert.Equal(PurchaseOrderStatus.Sent, cancelledEvent.PreviousStatus);
    }

    [Fact]
    public void Cancel_FromPartiallyReceived_ShouldTransitionToCancelledAndEmitDomainEvent()
    {
        // Arrange
        Guid productId = Guid.NewGuid();
        var lines = new List<(Guid ProductId, decimal QuantityOrdered, Money UnitCost)>
        {
            (productId, 10m, Money.Create(40m, "USD"))
        };
        var order = PurchaseOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-007", lines, _timeProvider);
        order.Send(_timeProvider);
        order.ReceiveLines(new[] { (productId, 5m) }, _timeProvider);
        order.ClearDomainEvents();

        // Act
        order.Cancel(_timeProvider);

        // Assert
        Assert.Equal(PurchaseOrderStatus.Cancelled, order.Status);
        
        var cancelledEvent = order.DomainEvents.OfType<DomainEvents.PurchaseOrderCancelledDomainEvent>().SingleOrDefault();
        Assert.NotNull(cancelledEvent);
        Assert.Equal(order.Id, cancelledEvent.PurchaseOrderId);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, cancelledEvent.PreviousStatus);
    }

    [Fact]
    public void Cancel_FromReceived_ShouldThrowDomainExceptionAndNotEmitEvent()
    {
        // Arrange
        Guid productId = Guid.NewGuid();
        var lines = new List<(Guid ProductId, decimal QuantityOrdered, Money UnitCost)>
        {
            (productId, 10m, Money.Create(40m, "USD"))
        };
        var order = PurchaseOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-008", lines, _timeProvider);
        order.Send(_timeProvider);
        order.ReceiveLines(new[] { (productId, 10m) }, _timeProvider);
        order.ClearDomainEvents();

        // Act & Assert
        Assert.Throws<DomainException>(() => order.Cancel(_timeProvider));
        Assert.Empty(order.DomainEvents.OfType<DomainEvents.PurchaseOrderCancelledDomainEvent>());
    }

    [Fact]
    public void Cancel_FromCancelled_ShouldThrowDomainExceptionAndNotEmitEvent()
    {
        // Arrange
        var lines = new List<(Guid ProductId, decimal QuantityOrdered, Money UnitCost)>
        {
            (Guid.NewGuid(), 10m, Money.Create(40m, "USD"))
        };
        var order = PurchaseOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-009", lines, _timeProvider);
        order.Cancel(_timeProvider);
        order.ClearDomainEvents();

        // Act & Assert
        Assert.Throws<DomainException>(() => order.Cancel(_timeProvider));
        Assert.Empty(order.DomainEvents.OfType<DomainEvents.PurchaseOrderCancelledDomainEvent>());
    }
}
