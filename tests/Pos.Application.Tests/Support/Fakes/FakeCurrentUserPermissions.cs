namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Authorization;

public sealed class FakeCurrentUserPermissions : ICurrentUserPermissions
{
    /// <summary>Conjunto de permisos otorgados al usuario actual en pruebas.</summary>
    public HashSet<string> PermissionsSet { get; } = [];

    /// <summary>Otorga uno o más permisos al usuario actual para la prueba.</summary>
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
