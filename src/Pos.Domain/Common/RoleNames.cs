using System.Collections.Frozen;

namespace Pos.Domain.Common;

/// <summary>
/// Fuente única de verdad para los nombres reservados de roles del sistema.
/// </summary>
public static class RoleNames
{
    public static readonly FrozenSet<string> TenantSystem = FrozenSet.ToFrozenSet(
        [
            TenantRoles.TenantAdmin,
            TenantRoles.Supervisor,
            TenantRoles.InventoryManager,
            TenantRoles.Cajero
        ],
        StringComparer.OrdinalIgnoreCase);

    public static readonly FrozenSet<string> Reserved = FrozenSet.ToFrozenSet(
        [
            PlatformRoles.SuperAdmin,
            TenantRoles.TenantAdmin,
            TenantRoles.Supervisor,
            TenantRoles.InventoryManager,
            TenantRoles.Cajero
        ],
        StringComparer.OrdinalIgnoreCase);

    public static bool IsReserved(string? name)
        => !string.IsNullOrWhiteSpace(name) && Reserved.Contains(name.Trim());

    public static bool IsTenantSystemRole(string? name)
        => !string.IsNullOrWhiteSpace(name) && TenantSystem.Contains(name.Trim());
}

