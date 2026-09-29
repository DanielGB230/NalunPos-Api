using System;

namespace Pos.Domain.Common;

/// <summary>
/// Interfaz para eventos de dominio que están explícitamente dirigidos a un tenant.
/// Útil para que el sistema de Outbox resuelva el TenantId directamente desde el evento
/// cuando el agregado no implementa ITenantOwnedEntity o IOptionalTenantOwnedEntity.
/// </summary>
public interface ITenantTargetedDomainEvent : IDomainEvent
{
    Guid TenantId { get; }
}
