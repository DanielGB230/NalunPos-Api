using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

/// <summary>
/// Evento de dominio emitido cuando cambia el rol o el tenant de un Usuario.
/// </summary>
public record UserRoleChangedDomainEvent(
    Guid UserId,
    Guid OldRoleId,
    Guid NewRoleId,
    Guid? OldTenantId,
    Guid? NewTenantId,
    DateTime OccurredOnUtc
) : IDomainEvent;
