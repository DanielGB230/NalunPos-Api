using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public record RoleCreatedDomainEvent(Guid RoleId, string Name, DateTime OccurredOnUtc) : IDomainEvent;
