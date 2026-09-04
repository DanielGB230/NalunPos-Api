using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public record CategoryCreatedDomainEvent(Guid CategoryId, string Name, DateTime OccurredOnUtc) : IDomainEvent;

public record CategoryUpdatedDomainEvent(Guid CategoryId, string Name, DateTime OccurredOnUtc) : IDomainEvent;

public record CategoryDeactivatedDomainEvent(Guid CategoryId, DateTime OccurredOnUtc) : IDomainEvent;
