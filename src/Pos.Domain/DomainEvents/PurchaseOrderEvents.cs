using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public record PurchaseOrderCreatedDomainEvent(
    Guid PurchaseOrderId,
    Guid TenantId,
    Guid SupplierId,
    Guid WarehouseId,
    string OrderNumber,
    DateTime OccurredOnUtc
) : IDomainEvent;

public record PurchaseOrderReceivedDomainEvent(
    Guid PurchaseOrderId,
    string OrderNumber,
    Guid WarehouseId,
    DateTime OccurredOnUtc
) : IDomainEvent;
