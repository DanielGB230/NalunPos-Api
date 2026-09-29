using System;
using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public sealed record UserStatusChangedDomainEvent(
    Guid UserId,
    Guid? TenantId,
    bool IsActive,
    DateTime OccurredOnUtc) : IDomainEvent;
