using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly PosDbContext _context;

    public CategoryRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        string normalized = name.Trim();
        return await _context.Categories.FirstOrDefaultAsync(c => EF.Functions.Like(c.Name, normalized), cancellationToken);
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Categories.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Category> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm,
        bool? isActiveOnly,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = includeInactive
            ? _context.Categories.IgnoreQueryFilters().AsNoTracking().AsQueryable()
            : _context.Categories.AsNoTracking().AsQueryable();

        if (!includeInactive && isActiveOnly.HasValue)
        {
            query = query.Where(c => c.IsActive == isActiveOnly.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            string pattern = $"%{searchTerm.Trim()}%";
            query = query.Where(c => EF.Functions.Like(c.Name, pattern) || (c.Description != null && EF.Functions.Like(c.Description, pattern)));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        string normalized = name.Trim();
        return await _context.Categories.AnyAsync(c => EF.Functions.Like(c.Name, normalized) && (excludeId == null || c.Id != excludeId.Value), cancellationToken);
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        await _context.Categories.AddAsync(category, cancellationToken);
    }

    public void Update(Category category)
    {
        _context.Categories.Update(category);
    }

    public void Delete(Category category)
    {
        _context.Categories.Remove(category);
    }
}
