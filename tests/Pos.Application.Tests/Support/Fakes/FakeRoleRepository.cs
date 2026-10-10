namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Entities;
using Pos.Domain.Interfaces;

public sealed class FakeRoleRepository : IRoleRepository
{
    public List<Role> Roles { get; } = [];

    public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Role>>(Roles);
    public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Roles.FirstOrDefault(r => r.Id == id));
    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Roles.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
    public Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(Roles.Any(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && r.Id != excludeId));
    public Task AddAsync(Role role, CancellationToken cancellationToken = default) { Roles.Add(role); return Task.CompletedTask; }
    public void Update(Role role) { }
    public void Delete(Role role) => Roles.Remove(role);
    public Task<(IReadOnlyList<Role> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm = null, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<Role>, int)>((Roles, Roles.Count));
}
