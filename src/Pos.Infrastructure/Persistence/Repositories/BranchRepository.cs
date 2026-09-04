using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Repositories;

public class BranchRepository : IBranchRepository
{
    private readonly PosDbContext _context;

    public BranchRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Branch>> GetAllAsync(bool? isActiveOnly = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Branches.AsNoTracking().AsQueryable();

        if (isActiveOnly.HasValue)
        {
            query = query.Where(b => b.IsActive == isActiveOnly.Value);
        }

        return await query.OrderBy(b => b.Name).ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        string normalized = name.Trim();
        return await _context.Branches.AnyAsync(b => EF.Functions.Like(b.Name, normalized) && (excludeId == null || b.Id != excludeId.Value), cancellationToken);
    }

    public async Task AddAsync(Branch branch, CancellationToken cancellationToken = default)
    {
        await _context.Branches.AddAsync(branch, cancellationToken);
    }

    public void Update(Branch branch)
    {
        _context.Branches.Update(branch);
    }
}
