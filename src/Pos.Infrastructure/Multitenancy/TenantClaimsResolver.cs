using System.Security.Claims;
using Pos.Domain.Common;

namespace Pos.Infrastructure.Multitenancy;

/// <summary>
/// Fuente única de parseo de los claims de tenant e identidad de plataforma
/// presentes en un <see cref="ClaimsPrincipal"/> autenticado.
/// Solo lee datos; nunca escribe estado.
/// </summary>
public static class TenantClaimsResolver
{
    /// <summary>
    /// Extrae el contexto de tenant e identidad SuperAdmin desde las claims del usuario
    /// y, opcionalmente, desde los items del HttpContext ya populados por middleware previo.
    /// </summary>
    /// <param name="user">Principal autenticado.</param>
    /// <param name="items">
    ///     Items del HttpContext actuales (puede ser <c>null</c> para contextos sin HTTP).
    ///     Si ya contiene la clave <c>"TenantId"</c> de tipo <see cref="Guid"/>, se usa
    ///     directamente como fuente autoritativa.
    /// </param>
    /// <returns>
    ///     Tupla con el <c>TenantId</c> resuelto (o <c>null</c> para plataforma) y si es
    ///     SuperAdmin de plataforma (<c>true</c> solo cuando no tiene tenant asignado).
    /// </returns>
    public static (Guid? TenantId, bool IsSuperAdmin) Resolve(
        ClaimsPrincipal user,
        IDictionary<object, object?>? items)
    {
        ArgumentNullException.ThrowIfNull(user);

        // 1. Items del HttpContext tienen prioridad (ya validados por TenantResolutionMiddleware).
        if (items is not null &&
            items.TryGetValue("TenantId", out var obj) &&
            obj is Guid guidFromItems &&
            guidFromItems != Guid.Empty)
        {
            return (guidFromItems, false);
        }

        // 2. Claims JWT: variantes normalizadas del claim de tenant.
        var tenantClaim = user.FindFirst(c =>
            c.Type.Equals("tenantId", StringComparison.OrdinalIgnoreCase) ||
            c.Type.Equals("tenant_id", StringComparison.OrdinalIgnoreCase) ||
            c.Type.Equals("tid", StringComparison.OrdinalIgnoreCase))?.Value;

        Guid? tenantId = null;
        if (!string.IsNullOrWhiteSpace(tenantClaim) &&
            Guid.TryParse(tenantClaim, out var parsed) &&
            parsed != Guid.Empty)
        {
            tenantId = parsed;
        }

        // 3. Es SuperAdmin de plataforma solo si tiene el rol y NO tiene tenant asignado (fail-closed).
        bool isSuperAdmin = user.IsInRole(PlatformRoles.SuperAdmin) && tenantId is null;

        return (tenantId, isSuperAdmin);
    }
}

