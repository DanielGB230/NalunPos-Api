using Pos.Application.Common.Authorization;

namespace Pos.IntegrationTests.Support;

/// <summary>
/// Implementación falsa y fail-closed de <see cref="ICurrentUserPermissions"/> para pruebas de integración.
/// Por diseño no concede permisos a menos que sean explícitamente configurados para la prueba.
/// </summary>
public sealed class FakeCurrentUserPermissions : ICurrentUserPermissions
{
    private readonly HashSet<string> _grantedPermissions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Concede uno o varios permisos específicos al contexto de ejecución de la prueba.
    /// </summary>
    /// <param name="permissions">Lista de permisos a otorgar.</param>
    /// <returns>La instancia actual para encadenamiento fluido.</returns>
    public FakeCurrentUserPermissions Grant(params string[] permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        foreach (var permission in permissions)
        {
            if (!string.IsNullOrWhiteSpace(permission))
            {
                _grantedPermissions.Add(permission.Trim());
            }
        }

        return this;
    }

    /// <summary>
    /// Obtiene el conjunto de permisos autorizados. Fail-closed: solo retorna los permisos explícitamente concedidos.
    /// </summary>
    public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid? tenantId, Guid roleId, CancellationToken cancellationToken = default)
    {
        IReadOnlySet<string> result = new HashSet<string>(_grantedPermissions, StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Evalúa si un permiso específico ha sido concedido. Fail-closed: falso por defecto.
    /// </summary>
    public Task<bool> HasPermissionAsync(Guid? tenantId, Guid roleId, string permission, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(_grantedPermissions.Contains(permission.Trim()));
    }
}
