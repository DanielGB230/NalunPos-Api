using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public record BranchCreatedDomainEvent(Guid BranchId, string Name, DateTime OccurredOnUtc) : IDomainEvent;

public record BranchUpdatedDomainEvent(Guid BranchId, string Name, DateTime OccurredOnUtc) : IDomainEvent;
