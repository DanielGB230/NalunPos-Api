namespace Pos.Domain.Enums;

/// <summary>
/// Roles de usuario dentro del sistema POS Multi-tenant.
/// </summary>
public enum UserRole
{
    SuperAdmin,
    TenantAdmin,
    Cajero,
    InventoryManager,
    Supervisor
}
