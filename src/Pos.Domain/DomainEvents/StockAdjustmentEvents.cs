using Pos.Domain.Common;
using Pos.Domain.Enums;

namespace Pos.Domain.DomainEvents;

public record StockAdjustmentCreatedDomainEvent(
    Guid StockAdjustmentId,
    Guid TenantId,
    Guid WarehouseId,
    StockAdjustmentReason Reason,
    DateTime OccurredOnUtc
) : IDomainEvent;
