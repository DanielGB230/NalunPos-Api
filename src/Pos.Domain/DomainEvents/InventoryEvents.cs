using Pos.Domain.Common;
using Pos.Domain.Enums;

namespace Pos.Domain.DomainEvents;

public record InventoryMovementRecordedDomainEvent(
    Guid MovementId,
    Guid ProductId,
    decimal Quantity,
    InventoryMovementType MovementType,
    Guid? ReferenceId,
    DateTime OccurredOnUtc
) : IDomainEvent;
