using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public record NotificationCreatedDomainEvent(Guid NotificationId, Guid UserId, string Title, DateTime OccurredOnUtc) : IDomainEvent;
