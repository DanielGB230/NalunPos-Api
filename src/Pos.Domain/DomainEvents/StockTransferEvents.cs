using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public record StockTransferCreatedDomainEvent(
    Guid StockTransferId,
    Guid TenantId,
    Guid SourceWarehouseId,
    Guid DestinationWarehouseId,
    DateTime OccurredOnUtc
) : IDomainEvent;
