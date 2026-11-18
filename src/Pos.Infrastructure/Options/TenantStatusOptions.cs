using System.ComponentModel.DataAnnotations;

namespace Pos.Infrastructure;

/// <summary>
/// Opciones de configuración para el control de acceso por estado de tenant.
/// </summary>
public class TenantStatusOptions
{
    public const string SectionName = "TenantStatus";

    /// <summary>
    /// TTL (en segundos) de la caché de estado de tenant usada por <see cref="ITenantStatusReader"/>.
    /// Define el retardo máximo de propagación de un cambio de estado (suspender/reactivar)
    /// hasta que surta efecto en las peticiones autenticadas. Mismo criterio que los permisos (ADR 0015).
    /// </summary>
    [Range(1, 3600, ErrorMessage = "El TTL de caché de estado de tenant debe estar entre 1 y 3600 segundos.")]
    public int TenantStatusCacheTtlSeconds { get; set; } = 60;
}
