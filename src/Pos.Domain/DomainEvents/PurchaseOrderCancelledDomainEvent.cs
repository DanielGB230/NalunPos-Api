using System;
using Pos.Domain.Common;
using Pos.Domain.Enums;

namespace Pos.Domain.DomainEvents;

public record PurchaseOrderCancelledDomainEvent(
    Guid PurchaseOrderId,
    Guid TenantId,
    Guid SupplierId,
    Guid WarehouseId,
    string OrderNumber,
    PurchaseOrderStatus PreviousStatus,
    DateTime OccurredOnUtc
) : IDomainEvent;
