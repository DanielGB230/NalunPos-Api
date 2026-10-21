using System;
using Pos.Domain.Common;

namespace Pos.Infrastructure.Caching;

/// <summary>
/// Centralizador de construcción determinista de claves de caché prefijadas por TenantId.
/// Conforme a ADR 0011 (Claves de Caché Tenant-Aware).
/// </summary>
public static class TenantCacheKeys
{
    /// <summary>
    /// Construye la clave de caché para los permisos de un rol específico dentro de un tenant.
    /// Valida que el identificador de tenant sea válido con <see cref="TenantGuard.EnsureNotEmpty"/>.
    /// </summary>
    public static string PermissionsForRole(Guid tenantId, Guid roleId)
    {
        TenantGuard.EnsureNotEmpty(tenantId, nameof(tenantId));
        return $"tenant:{tenantId:D}:role:{roleId:D}:permissions";
    }

    /// <summary>
    /// Construye la clave de caché para permisos de alcance global de plataforma.
    /// </summary>
    public static string GlobalPermissionsForRole(Guid roleId)
    {
        return $"tenant:global:role:{roleId:D}:permissions";
    }

    /// <summary>
    /// Construye la clave de caché para un rol soportando inquilino específico o alcance global.
    /// </summary>
    public static string PermissionsForRole(Guid? tenantId, Guid roleId)
    {
        if (!tenantId.HasValue || tenantId.Value == PlatformScope.TenantId)
        {
            return GlobalPermissionsForRole(roleId);
        }

        return PermissionsForRole(tenantId.Value, roleId);
    }
}
