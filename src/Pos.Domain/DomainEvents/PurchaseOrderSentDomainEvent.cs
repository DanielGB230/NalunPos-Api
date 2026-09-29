using System;
using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public sealed record PurchaseOrderSentDomainEvent(
    Guid PurchaseOrderId,
    Guid TenantId,
    Guid SupplierId,
    Guid WarehouseId,
    string OrderNumber,
    DateTime OccurredOnUtc) : IDomainEvent;
