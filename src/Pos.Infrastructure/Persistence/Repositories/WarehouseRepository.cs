using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Repositories;

public class WarehouseRepository : IWarehouseRepository
{
    private readonly PosDbContext _context;

    public WarehouseRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(warehouse);
        await _context.Warehouses.AddAsync(warehouse, cancellationToken);
    }

    public async Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<Warehouse?> GetDefaultAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Warehouses.FirstOrDefaultAsync(w => w.IsDefault && w.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<Warehouse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Warehouses
            .AsNoTracking()
            .OrderByDescending(w => w.IsDefault)
            .ThenBy(w => w.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByTenantAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Warehouses.CountAsync(cancellationToken);
    }

    public void Update(Warehouse warehouse)
    {
        ArgumentNullException.ThrowIfNull(warehouse);
        _context.Warehouses.Update(warehouse);
    }
}
