using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public record CashRegisterSessionOpenedDomainEvent(
    Guid SessionId,
    Guid CashRegisterId,
    Guid UserId,
    decimal InitialAmount,
    string Currency,
    DateTime OccurredOnUtc
) : IDomainEvent;

public record CashRegisterSessionClosedDomainEvent(
    Guid SessionId,
    Guid CashRegisterId,
    decimal ExpectedFinalAmount,
    decimal ActualFinalAmount,
    string Currency,
    DateTime OccurredOnUtc
) : IDomainEvent;
