using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado inmutable (Append-Only / Kardex) para registrar cada movimiento físico de inventario.
/// Los registros de Kardex nunca se modifican ni eliminan.
/// </summary>
public class InventoryMovement : AggregateRoot<Guid>
{
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public InventoryMovementType MovementType { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // Constructor privado para EF Core
    private InventoryMovement()
    {
    }

    private InventoryMovement(
        Guid id,
        Guid productId,
        decimal quantity,
        InventoryMovementType movementType,
        Guid? referenceId,
        string? notes) : base(id)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("El ID del producto es requerido para registrar un movimiento de inventario.");
        }

        if (quantity == 0)
        {
            throw new DomainException("La cantidad del movimiento de inventario no puede ser cero.");
        }

        ProductId = productId;
        Quantity = quantity;
        MovementType = movementType;
        ReferenceId = referenceId;
        Notes = notes?.Trim();
        CreatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new InventoryMovementRecordedDomainEvent(
            Id,
            ProductId,
            Quantity,
            MovementType,
            ReferenceId,
            CreatedAtUtc));
    }

    public static InventoryMovement Record(
        Guid productId,
        decimal quantity,
        InventoryMovementType movementType,
        Guid? referenceId = null,
        string? notes = null)
    {
        return new InventoryMovement(Guid.NewGuid(), productId, quantity, movementType, referenceId, notes);
    }
}
