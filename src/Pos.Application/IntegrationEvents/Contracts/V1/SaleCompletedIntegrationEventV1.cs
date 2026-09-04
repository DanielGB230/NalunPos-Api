namespace Pos.Application.IntegrationEvents.Contracts.V1;

public record SaleCompletedIntegrationEventV1(
    Guid Id,
    Guid SaleId,
    string ReceiptNumber,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset OccurredOnUtc
) : IIntegrationEvent;
