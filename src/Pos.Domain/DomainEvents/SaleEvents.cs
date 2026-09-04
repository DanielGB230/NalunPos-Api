using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public record SaleCompletedItem(
    Guid ProductId,
    decimal Quantity,
    decimal UnitPriceAmount,
    string Currency
);

public record SaleCompletedDomainEvent(
    Guid SaleId,
    string ReceiptNumber,
    Guid SessionId,
    Guid? CustomerId,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<SaleCompletedItem> Items,
    DateTime OccurredOnUtc
) : IDomainEvent;

public record SaleCancelledDomainEvent(
    Guid SaleId,
    string ReceiptNumber,
    DateTime OccurredOnUtc
) : IDomainEvent;
