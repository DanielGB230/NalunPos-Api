namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Entities;
using Pos.Domain.Interfaces;

public sealed class FakeCategoryRepository : ICategoryRepository
{
    public List<Category> Categories { get; } = [];
    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Categories.FirstOrDefault(c => c.Id == id));
    public Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Categories.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
    public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Category>>(Categories);
    public Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(Categories.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && c.Id != excludeId));
    public Task AddAsync(Category category, CancellationToken cancellationToken = default) { Categories.Add(category); return Task.CompletedTask; }
    public void Update(Category category) { }
    public void Delete(Category category) => Categories.Remove(category);
    public Task<(IReadOnlyList<Category> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm, bool? isActive = null, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<Category>, int)>((Categories, Categories.Count));
}
