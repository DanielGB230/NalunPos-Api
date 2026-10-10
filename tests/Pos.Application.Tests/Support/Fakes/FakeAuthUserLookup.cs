namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;

public sealed class FakeAuthUserLookup : IAuthUserLookup
{
    public List<User> Users
    {
        get => _users;
        set => _users = value;
    }
    private List<User> _users = [];

    public FakeAuthUserLookup() { }

    public FakeAuthUserLookup(List<User> users)
    {
        _users = users;
    }

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        string normalized = email.Trim().ToLowerInvariant();
        return Task.FromResult(_users.FirstOrDefault(u => u.Email.Value == normalized));
    }

    public Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        string normalized = email.Trim().ToLowerInvariant();
        return Task.FromResult(_users.Any(u => u.Email.Value == normalized && (excludeId == null || u.Id != excludeId.Value)));
    }
}
