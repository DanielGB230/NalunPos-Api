using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public record InvoiceIssuedDomainEvent(
    Guid InvoiceId,
    Guid SaleId,
    string DocumentNumber,
    string DocumentType,
    decimal TotalAmount,
    string Currency,
    DateTime OccurredOnUtc
) : IDomainEvent;

public record InvoiceStatusUpdatedDomainEvent(
    Guid InvoiceId,
    string DocumentNumber,
    string Status,
    DateTime OccurredOnUtc
) : IDomainEvent;
