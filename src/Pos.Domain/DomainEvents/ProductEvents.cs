using Pos.Domain.Common;

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

public sealed record ProductStatusChangedDomainEvent(
    Guid ProductId,
    Guid TenantId,
    bool IsActive,
    DateTime OccurredOnUtc
) : IDomainEvent;
