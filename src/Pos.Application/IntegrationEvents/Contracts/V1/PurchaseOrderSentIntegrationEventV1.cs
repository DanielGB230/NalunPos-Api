using System;

namespace Pos.Application.IntegrationEvents.Contracts.V1;

public record PurchaseOrderSentIntegrationEventV1(
    Guid Id,
    Guid PurchaseOrderId,
    Guid TenantId,
    Guid SupplierId,
    Guid WarehouseId,
    string OrderNumber,
    DateTimeOffset OccurredOnUtc
) : IIntegrationEvent;
