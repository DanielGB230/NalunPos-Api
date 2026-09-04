using Pos.Domain.Common;

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
