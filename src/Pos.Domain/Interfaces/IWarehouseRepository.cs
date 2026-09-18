using Pos.Domain.Entities;

namespace Pos.Domain.Interfaces;

public interface IWarehouseRepository
{
    Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default);
    Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Warehouse?> GetDefaultAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Warehouse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> CountByTenantAsync(CancellationToken cancellationToken = default);
    void Update(Warehouse warehouse);
}
