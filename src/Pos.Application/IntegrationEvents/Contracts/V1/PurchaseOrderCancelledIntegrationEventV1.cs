using System;

namespace Pos.Application.IntegrationEvents.Contracts.V1;

public record PurchaseOrderCancelledIntegrationEventV1(
    Guid Id,
    Guid PurchaseOrderId,
    Guid TenantId,
    Guid SupplierId,
    Guid WarehouseId,
    string OrderNumber,
    string PreviousStatus,
    DateTimeOffset OccurredOnUtc
) : IIntegrationEvent;
