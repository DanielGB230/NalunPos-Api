namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Entities;

public sealed class FakeAuthUserLookup : IAuthUserLookup
{
    private List<User> _users = [];
    private readonly Dictionary<Guid, string> _roleNames = new();

    public List<User> Users
    {
        get => _users;
        set => _users = value;
    }

    public Dictionary<Guid, string> RoleNames => _roleNames;

    public FakeAuthUserLookup() { }

    public FakeAuthUserLookup(List<User> users)
    {
        _users = users;
    }

    public void SetRoleName(Guid roleId, string roleName)
    {
        _roleNames[roleId] = roleName;
    }

    public Task<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        string normalized = email.Trim().ToLowerInvariant();
        var user = _users.FirstOrDefault(u => u.Email.Value == normalized);
        if (user is null)
        {
            return Task.FromResult<AuthUser?>(null);
        }

        string role = ResolveRoleName(user);
        if (role == "__CORRUPTED__")
        {
            return Task.FromResult<AuthUser?>(null);
        }

        return Task.FromResult<AuthUser?>(new AuthUser(user, role));
    }

    public Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        string normalized = email.Trim().ToLowerInvariant();
        return Task.FromResult(_users.Any(u => u.Email.Value == normalized && (excludeId == null || u.Id != excludeId.Value)));
    }

    private string ResolveRoleName(User user)
    {
        if (user.RoleId == Role.SuperAdminRoleId && !user.TenantId.HasValue)
        {
            return PlatformRoles.SuperAdmin;
        }

        if (user.RoleId == Role.SuperAdminRoleId || !user.TenantId.HasValue)
        {
            return "__CORRUPTED__";
        }

        if (_roleNames.TryGetValue(user.RoleId, out var customRole))
        {
            return customRole;
        }

        return TenantRoles.TenantAdmin;
    }
}
