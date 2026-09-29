using System;
using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public sealed record ProductStatusChangedDomainEvent(
    Guid ProductId,
    Guid TenantId,
    bool IsActive,
    DateTime OccurredOnUtc) : IDomainEvent;
