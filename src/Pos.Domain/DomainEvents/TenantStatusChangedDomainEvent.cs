using System;
using Pos.Domain.Common;
using Pos.Domain.Enums;

namespace Pos.Domain.DomainEvents;

public sealed record TenantStatusChangedDomainEvent(
    Guid TenantId,
    TenantStatus OldStatus,
    TenantStatus NewStatus,
    DateTime OccurredOnUtc) : ITenantTargetedDomainEvent;
