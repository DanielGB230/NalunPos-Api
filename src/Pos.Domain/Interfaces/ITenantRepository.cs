using Pos.Domain.Entities;

namespace Pos.Domain.Interfaces;

/// <summary>
/// Contrato del repositorio de Tenants (Agregado de Dominio).
/// </summary>
public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByTaxIdAsync(string taxId, CancellationToken cancellationToken = default);
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
    void Update(Tenant tenant);
    Task<(IReadOnlyList<Tenant> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm,
        CancellationToken cancellationToken = default);
}
