using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public record UserRoleAssignedDomainEvent(Guid UserId, Guid RoleId, DateTime OccurredOnUtc) : IDomainEvent;
