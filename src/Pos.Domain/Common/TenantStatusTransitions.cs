using Pos.Domain.Enums;

namespace Pos.Domain.Common;

/// <summary>
/// Fuente única de verdad de las transiciones permitidas del ciclo de vida de un Tenant.
/// Centraliza las reglas y los códigos de error estables para que el dominio y los casos de uso
/// no dupliquen la matriz de estados.
/// </summary>
/// <remarks>
/// Transiciones permitidas:
/// <list type="bullet">
/// <item><description><see cref="TenantStatus.PendingProvisioning"/> → <see cref="TenantStatus.Active"/></description></item>
/// <item><description><see cref="TenantStatus.Active"/> → <see cref="TenantStatus.Suspended"/></description></item>
/// <item><description><see cref="TenantStatus.Suspended"/> → <see cref="TenantStatus.Active"/></description></item>
/// </list>
/// <see cref="TenantStatus.PendingProvisioning"/> → <see cref="TenantStatus.Suspended"/> no está permitida:
/// un tenant debe activarse antes de poder suspenderse.
/// </remarks>
public static class TenantStatusTransitions
{
    /// <summary>Código estable para una transición de estado no permitida.</summary>
    public const string InvalidTransitionCode = "Tenant.InvalidTransition";

    /// <summary>Código estable para intentar activar un tenant ya activo.</summary>
    public const string AlreadyActiveCode = "Tenant.AlreadyActive";

    /// <summary>Código estable para intentar suspender un tenant ya suspendido.</summary>
    public const string AlreadySuspendedCode = "Tenant.AlreadySuspended";

    /// <summary>
    /// Valida si un tenant puede transicionar al estado <see cref="TenantStatus.Active"/>.
    /// </summary>
    /// <param name="currentStatus">Estado actual del tenant.</param>
    /// <returns><c>null</c> si la transición es válida; en caso contrario, el error de conflicto estable.</returns>
    public static DomainError? ValidateActivation(TenantStatus currentStatus) => currentStatus switch
    {
        TenantStatus.PendingProvisioning => null,
        TenantStatus.Suspended => null,
        TenantStatus.Active => DomainError.Conflict(
            AlreadyActiveCode,
            "El tenant ya se encuentra activo."),
        _ => DomainError.Conflict(
            InvalidTransitionCode,
            $"Transición de estado no permitida desde '{currentStatus}' hacia '{TenantStatus.Active}'.")
    };

    /// <summary>
    /// Valida si un tenant puede transicionar al estado <see cref="TenantStatus.Suspended"/>.
    /// </summary>
    /// <param name="currentStatus">Estado actual del tenant.</param>
    /// <returns><c>null</c> si la transición es válida; en caso contrario, el error de conflicto estable.</returns>
    public static DomainError? ValidateSuspension(TenantStatus currentStatus) => currentStatus switch
    {
        TenantStatus.Active => null,
        TenantStatus.Suspended => DomainError.Conflict(
            AlreadySuspendedCode,
            "El tenant ya se encuentra suspendido."),
        TenantStatus.PendingProvisioning => DomainError.Conflict(
            InvalidTransitionCode,
            "Un tenant en aprovisionamiento debe activarse antes de poder suspenderse."),
        _ => DomainError.Conflict(
            InvalidTransitionCode,
            $"Transición de estado no permitida desde '{currentStatus}' hacia '{TenantStatus.Suspended}'.")
    };
}
