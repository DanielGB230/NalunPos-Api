namespace Pos.Application.Common.Authorization;

/// <summary>
/// Catálogo canónico y fuente única de verdad de los permisos asignados al rol SuperAdmin (Platform).
/// Principio de mínimo privilegio: contiene única y exclusivamente los permisos requeridos por los
/// casos de uso de plataforma existentes con endpoint activo en el sistema.
/// </summary>
public static class SuperAdminRoleCatalog
{
    public static IReadOnlySet<string> Permissions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Common.Authorization.Permissions.Platform.DeadLetterView,
        Common.Authorization.Permissions.Platform.DeadLetterReprocess,
        Common.Authorization.Permissions.Platform.Tenants.View,
        Common.Authorization.Permissions.Platform.Tenants.Create,
        Common.Authorization.Permissions.Platform.Tenants.Update
    };
}

