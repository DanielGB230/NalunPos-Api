namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Entities;
using Pos.Domain.Interfaces;

public sealed class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];
    public User? UserToReturn { get; set; }
    public int LastRequestedPageSize { get; private set; }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(UserToReturn ?? Users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        string normalized = email.Trim().ToLowerInvariant();
        return Task.FromResult(UserToReturn ?? Users.FirstOrDefault(u => u.Email.Value.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        string normalized = email.Trim().ToLowerInvariant();
        return Task.FromResult(Users.Any(u => u.Email.Value.Equals(normalized, StringComparison.OrdinalIgnoreCase) && (excludeId == null || u.Id != excludeId.Value)));
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        Users.Add(user);
        return Task.CompletedTask;
    }

    public void Update(User user) { }

    public Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        LastRequestedPageSize = pageSize;
        return Task.FromResult<(IReadOnlyList<User>, int)>((Users, Users.Count));
    }
}
