using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public record PurchaseCompletedItem(
    Guid ProductId,
    decimal Quantity,
    decimal UnitPriceAmount,
    string Currency
);

public record PurchaseCompletedDomainEvent(
    Guid PurchaseId,
    Guid SupplierId,
    string OrderNumber,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<PurchaseCompletedItem> Items,
    DateTime OccurredOnUtc
) : IDomainEvent;

public record PurchaseCancelledDomainEvent(
    Guid PurchaseId,
    string OrderNumber,
    DateTime OccurredOnUtc
) : IDomainEvent;
