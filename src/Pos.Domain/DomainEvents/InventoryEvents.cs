using Pos.Domain.Common;
using Pos.Domain.Enums;

namespace Pos.Domain.DomainEvents;

/// <summary>
/// Evento emitido cada vez que se registra un movimiento en el Kardex de inventario.
/// El movimiento ya es inmutable desde que se crea — este evento confirma su registro.
/// </summary>
public record InventoryMovementRecordedDomainEvent(
    Guid MovementId,
    Guid ProductId,
    Guid WarehouseId,
    decimal Quantity,
    InventoryMovementType MovementType,
    Guid? ReferenceId,
    DateTime OccurredOnUtc
) : IDomainEvent;
