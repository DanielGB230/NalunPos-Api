using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public record CustomerCreatedDomainEvent(Guid CustomerId, string FullName, string TaxId, DateTime OccurredOnUtc) : IDomainEvent;

public record CustomerUpdatedDomainEvent(Guid CustomerId, string FullName, DateTime OccurredOnUtc) : IDomainEvent;
