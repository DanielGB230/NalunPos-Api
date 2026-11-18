using Pos.Domain.Enums;

namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Puerto de lectura del estado de un Tenant para decisiones de control de acceso.
/// Implementado en infraestructura con caché HybridCache para evitar lecturas repetidas
/// a la base de datos en cada petición autenticada.
/// </summary>
public interface ITenantStatusReader
{
    /// <summary>
    /// Obtiene el estado actual del tenant, o <c>null</c> si no existe.
    /// </summary>
    /// <param name="tenantId">Identificador del tenant.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns><see cref="TenantStatus"/> del tenant, o <c>null</c> si no existe.</returns>
    Task<TenantStatus?> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
