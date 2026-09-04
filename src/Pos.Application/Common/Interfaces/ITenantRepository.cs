using Pos.Domain.Entities;

namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Contrato del repositorio de Tenants para los casos de uso de Application.
/// </summary>
public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByTaxIdAsync(string taxId, CancellationToken cancellationToken = default);
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
    void Update(Tenant tenant);
}
