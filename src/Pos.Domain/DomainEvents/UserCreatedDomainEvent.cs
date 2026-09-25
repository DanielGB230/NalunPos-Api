using Pos.Domain.Common;
using Pos.Domain.Enums;

namespace Pos.Domain.DomainEvents;

/// <summary>
/// Evento de dominio emitido cuando se crea un nuevo Usuario.
/// </summary>
public record UserCreatedDomainEvent(
    Guid UserId,
    string Email,
    Guid RoleId,
    Guid? TenantId,
    DateTime OccurredOnUtc
) : IDomainEvent;
