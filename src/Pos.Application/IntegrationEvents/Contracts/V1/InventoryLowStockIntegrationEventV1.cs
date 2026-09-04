namespace Pos.Application.IntegrationEvents.Contracts.V1;

public record InventoryLowStockIntegrationEventV1(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal CurrentStock,
    DateTimeOffset OccurredOnUtc
) : IIntegrationEvent;
