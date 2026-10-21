using Pos.Domain.Common;
using Pos.Domain.Enums;

namespace Pos.Domain.DomainEvents;

/// <summary>
/// Evento de dominio emitido cuando se crea un nuevo Tenant.
/// </summary>
public record TenantCreatedDomainEvent(
    Guid TenantId,
    string Name,
    string TaxId,
    DateTime OccurredOnUtc
) : IDomainEvent;

public sealed record TenantStatusChangedDomainEvent(
    Guid TenantId,
    TenantStatus OldStatus,
    TenantStatus NewStatus,
    DateTime OccurredOnUtc
) : ITenantTargetedDomainEvent;
