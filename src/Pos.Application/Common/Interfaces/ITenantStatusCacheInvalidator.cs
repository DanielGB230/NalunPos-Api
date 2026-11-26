namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Puerto de invalidación de la caché de estado de un Tenant.
/// Se invoca únicamente después de que un cambio de estado se haya persistido con éxito,
/// para que las siguientes lecturas vean el estado actual sin esperar al TTL de la caché.
/// </summary>
public interface ITenantStatusCacheInvalidator
{
    /// <summary>
    /// Elimina de la caché la entrada de estado del tenant indicado.
    /// </summary>
    /// <param name="tenantId">Identificador del tenant cuyo estado cambió.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task InvalidateAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
