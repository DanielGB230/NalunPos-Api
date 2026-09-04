namespace Pos.Domain.Enums;

/// <summary>
/// Estado del Inquilino (Tenant) en su ciclo de vida SaaS.
/// </summary>
public enum TenantStatus
{
    PendingProvisioning,
    Active,
    Suspended
}
