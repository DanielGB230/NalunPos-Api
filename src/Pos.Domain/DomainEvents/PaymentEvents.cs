using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public record PaymentProcessedDomainEvent(
    Guid PaymentId,
    Guid SaleId,
    decimal Amount,
    string Currency,
    string Method,
    string? ExternalReference,
    DateTime OccurredOnUtc
) : IDomainEvent;

public record PaymentFailedDomainEvent(
    Guid PaymentId,
    Guid SaleId,
    string Reason,
    DateTime OccurredOnUtc
) : IDomainEvent;
