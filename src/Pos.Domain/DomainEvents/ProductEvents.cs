using Pos.Domain.Common;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.DomainEvents;

public record ProductCreatedDomainEvent(
    Guid ProductId,
    string Name,
    string Sku,
    decimal PriceAmount,
    string Currency,
    Guid CategoryId,
    DateTime OccurredOnUtc
) : IDomainEvent;

public record ProductPriceUpdatedDomainEvent(
    Guid ProductId,
    decimal OldPriceAmount,
    decimal NewPriceAmount,
    string Currency,
    DateTime OccurredOnUtc
) : IDomainEvent;

public record ProductUpdatedDomainEvent(
    Guid ProductId,
    string Name,
    DateTime OccurredOnUtc
) : IDomainEvent;


