namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Entities;
using Pos.Domain.Interfaces;

public sealed class FakeBranchRepository : IBranchRepository
{
    public List<Branch> Branches { get; } = [];

    public Task<IReadOnlyList<Branch>> GetAllAsync(bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = Branches.AsEnumerable();
        if (isActive.HasValue) query = query.Where(b => b.IsActive == isActive.Value);
        return Task.FromResult<IReadOnlyList<Branch>>(query.ToList());
    }

    public Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Branches.FirstOrDefault(b => b.Id == id));
    public Task<Branch?> GetByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Branches.FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
    public Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(Branches.Any(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && b.Id != excludeId));
    public Task AddAsync(Branch branch, CancellationToken cancellationToken = default) { Branches.Add(branch); return Task.CompletedTask; }
    public void Update(Branch branch) { }
    public void Delete(Branch branch) => Branches.Remove(branch);
    public Task<(IReadOnlyList<Branch> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm, bool? isActive, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<Branch>, int)>((Branches, Branches.Count));
}
