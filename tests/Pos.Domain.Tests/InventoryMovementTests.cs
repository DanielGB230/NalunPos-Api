using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Xunit;

namespace Pos.Domain.Tests;

public class InventoryMovementTests
{
    [Fact]
    public void RecordWithValidParametersShouldInstantiateMovementAndEmitEvent()
    {
        // Arrange
        Guid productId = Guid.NewGuid();
        Guid warehouseId = Guid.NewGuid();
        decimal quantity = 50m;

        // Act
        var movement = InventoryMovement.Record(
            productId,
            warehouseId,
            quantity,
            InventoryMovementType.Purchase,
            referenceId: null,
            notes: "Compra lote inicial");

        // Assert
        Assert.NotEqual(Guid.Empty, movement.Id);
        Assert.Equal(productId, movement.ProductId);
        Assert.Equal(warehouseId, movement.WarehouseId);
        Assert.Equal(quantity, movement.Quantity);
        Assert.Equal(InventoryMovementType.Purchase, movement.MovementType);
        Assert.Equal("Compra lote inicial", movement.Notes);
        Assert.Single(movement.DomainEvents);
    }

    [Fact]
    public void RecordWithZeroQuantityShouldThrowDomainException()
    {
        // Arrange & Act & Assert
        Assert.Throws<DomainException>(() =>
            InventoryMovement.Record(
                Guid.NewGuid(),
                Guid.NewGuid(),
                0m,
                InventoryMovementType.Adjustment));
    }
}
