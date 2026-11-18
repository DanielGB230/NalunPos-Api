using Pos.Domain.Common;
using Pos.Domain.Enums;

namespace Pos.Domain.Common;

/// <summary>
/// Fuente única de verdad de la regla de acceso de tenants por estado de ciclo de vida.
/// Centraliza la decisión de si un usuario de tenant puede autenticarse y operar,
/// y los códigos de error estables asociados, para que el dominio, la API y los middlewares
/// no dupliquen la lógica de autorización por estado de tenant.
/// </summary>
/// <remarks>
/// Reglas de acceso (fail-closed):
/// <list type="bullet">
/// <item><see cref="TenantStatus.Active"/> → acceso permitido (<c>null</c> sin error).</item>
/// <item><see cref="TenantStatus.Suspended"/> → denegado con código estable <c>Tenant.Suspended</c>.</item>
/// <item><see cref="TenantStatus.PendingProvisioning"/> → denegado con código estable <c>Tenant.NotActive</c>.</item>
/// <item>Valor de enum no reconocido o <c>null</c> (tenant inexistente) → denegado con <c>Tenant.NotActive</c>.</item>
/// </list>
/// El tipo de error es <see cref="ErrorType.Forbidden"/>, mapeado a HTTP 403 por <see cref="Pos.Api.Common.PosProblemDetailsFactory"/>.
/// </remarks>
public static class TenantAccessPolicy
{
    /// <summary>Código estable para acceso denegado porque el tenant no está activo.</summary>
    public const string NotActiveCode = "Tenant.NotActive";

    /// <summary>Código estable para acceso denegado porque el tenant está suspendido.</summary>
    public const string SuspendedCode = "Tenant.Suspended";

    /// <summary>
    /// Valida si un tenant puede autenticarse y operar según su estado de ciclo de vida.
    /// </summary>
    /// <param name="status">Estado del tenant consultado, o <c>null</c> si el tenant no existe.</param>
    /// <returns><c>null</c> si el acceso está permitido; el <see cref="DomainError"/> de denegación en caso contrario.</returns>
    public static DomainError? ValidateAccess(TenantStatus? status) => status switch
    {
        TenantStatus.Active => null,
        TenantStatus.Suspended => DomainError.Forbidden(
            SuspendedCode,
            "El tenant se encuentra suspendido. El acceso está denegado hasta que sea reactivado por un SuperAdmin."),
        TenantStatus.PendingProvisioning => DomainError.Forbidden(
            NotActiveCode,
            "El tenant aún no está activo. Complete el aprovisionamiento antes de iniciar sesión."),
        null => DomainError.Forbidden(
            NotActiveCode,
            "El tenant no existe. El acceso está denegado."),
        _ => DomainError.Forbidden(
            NotActiveCode,
            $"Estado de tenant no reconocido '{status}'. Acceso denegado por política de seguridad.")
    };
}
