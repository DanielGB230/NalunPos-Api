using Microsoft.AspNetCore.Http;
using Pos.Application.Common.Interfaces;

namespace Pos.Infrastructure.Multitenancy;

public interface ITenantSetter
{
    void SetTenantId(Guid? tenantId);
    void SetSuperAdmin(bool isSuperAdmin);
}

/// <summary>
/// Implementación concreta de <see cref="ICurrentTenantContext"/> que resuelve el contexto
/// del tenant en este orden de prioridad (sin efectos laterales en ningún getter):
/// <list type="number">
///   <item>Valores fijados explícitamente vía <see cref="ITenantSetter"/> (workers en background
///         y peticiones autenticadas cuyo <see cref="TenantResolutionMiddleware"/> ya llamó
///         <see cref="SetTenantId"/> / <see cref="SetSuperAdmin"/>).</item>
///   <item>Claims del <see cref="HttpContext"/> activo (petición HTTP en curso sin fijación previa;
///         lectura pura sin snapshot mutable).</item>
/// </list>
/// </summary>
public class CurrentTenantContext : ICurrentTenantContext, ITenantSetter
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private bool _hasManualTenantId;
    private Guid? _manualTenantId;
    private bool? _manualIsSuperAdmin;

    public CurrentTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // ── ITenantSetter ────────────────────────────────────────────────────────

    public void SetTenantId(Guid? tenantId)
    {
        _manualTenantId = tenantId;
        _hasManualTenantId = true;
        if (tenantId.HasValue)
        {
            // Si se fija un tenant real, el contexto no es SuperAdmin de plataforma.
            _manualIsSuperAdmin = false;
        }
    }

    public void SetSuperAdmin(bool isSuperAdmin)
    {
        _manualIsSuperAdmin = isSuperAdmin;
    }

    // ── ICurrentTenantContext ────────────────────────────────────────────────

    /// <summary>
    /// Getter puro (sin efecto lateral). Devuelve el TenantId fijado manualmente,
    /// o lo resuelve desde el HttpContext activo si no hay valor manual.
    /// </summary>
    public Guid? TenantId
    {
        get
        {
            if (_hasManualTenantId) return _manualTenantId;

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User.Identity?.IsAuthenticated == true)
                return TenantClaimsResolver.Resolve(httpContext.User, httpContext.Items).TenantId;

            return null;
        }
    }

    /// <summary>
    /// Getter puro (sin efecto lateral). Devuelve si el contexto es SuperAdmin de plataforma.
    /// </summary>
    public bool IsSuperAdmin
    {
        get
        {
            if (_manualIsSuperAdmin.HasValue) return _manualIsSuperAdmin.Value;

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User.Identity?.IsAuthenticated == true)
                return TenantClaimsResolver.Resolve(httpContext.User, httpContext.Items).IsSuperAdmin;

            return false;
        }
    }

    public bool HasTenant => TenantId.HasValue;
}
