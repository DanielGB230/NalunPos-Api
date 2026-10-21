using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public record UserRoleAssignedDomainEvent(
    Guid UserId,
    Guid RoleId,
    DateTime OccurredOnUtc
) : IDomainEvent;

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

public sealed record UserStatusChangedDomainEvent(
    Guid UserId,
    Guid? TenantId,
    bool IsActive,
    DateTime OccurredOnUtc
) : IDomainEvent;
