using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Repositories;

public class PosDeviceRepository : IPosDeviceRepository
{
    private readonly ApplicationDbContext _context;

    public PosDeviceRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PosDevice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PosDevices.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<PosDevice?> GetBySerialNumberAsync(string serialNumber, CancellationToken cancellationToken = default)
    {
        string normalized = serialNumber.Trim();
        return await _context.PosDevices.FirstOrDefaultAsync(d => EF.Functions.Like(d.SerialNumber, normalized), cancellationToken);
    }

    public async Task<IReadOnlyList<PosDevice>> GetByBranchIdAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        return await _context.PosDevices
            .AsNoTracking()
            .Where(d => d.BranchId == branchId)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsBySerialNumberAsync(string serialNumber, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        string normalized = serialNumber.Trim();
        return await _context.PosDevices.AnyAsync(d => EF.Functions.Like(d.SerialNumber, normalized) && (excludeId == null || d.Id != excludeId.Value), cancellationToken);
    }

    public async Task AddAsync(PosDevice device, CancellationToken cancellationToken = default)
    {
        await _context.PosDevices.AddAsync(device, cancellationToken);
    }

    public void Update(PosDevice device)
    {
        _context.PosDevices.Update(device);
    }
}
