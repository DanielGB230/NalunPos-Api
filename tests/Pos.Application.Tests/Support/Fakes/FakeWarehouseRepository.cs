namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Entities;
using Pos.Domain.Interfaces;

public sealed class FakeWarehouseRepository : IWarehouseRepository
{
    public List<Warehouse> Warehouses { get; } = [];
    public Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default) { Warehouses.Add(warehouse); return Task.CompletedTask; }
    public Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Warehouses.FirstOrDefault(w => w.Id == id));
    public Task<Warehouse?> GetDefaultAsync(CancellationToken cancellationToken = default) => Task.FromResult(Warehouses.FirstOrDefault(w => w.IsDefault));
    public Task<IReadOnlyList<Warehouse>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Warehouse>>(Warehouses);
    public Task<int> CountByTenantAsync(CancellationToken cancellationToken = default) => Task.FromResult(Warehouses.Count);
    public void Update(Warehouse warehouse) { }
}
