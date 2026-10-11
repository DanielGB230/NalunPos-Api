namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Authorization;

public sealed class FakeCurrentUserPermissions : ICurrentUserPermissions
{
    public HashSet<string> PermissionsSet { get; } = [];

    public void Grant(params string[] permissions)
    {
        foreach (var permission in permissions)
        {
            PermissionsSet.Add(permission);
        }
    }

    public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid? tenantId, Guid roleId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlySet<string>>(PermissionsSet);
    }

    public Task<bool> HasPermissionAsync(Guid? tenantId, Guid roleId, string permission, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PermissionsSet.Contains(permission));
    }
}
