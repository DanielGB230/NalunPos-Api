using Microsoft.AspNetCore.Http;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;

namespace Pos.Infrastructure.Multitenancy;

public interface ITenantSetter
{
    void SetTenantId(Guid? tenantId);
    void SetSuperAdmin(bool isSuperAdmin);
}

/// <summary>
/// Implementación concreta de ICurrentTenantContext que obtiene el contexto del tenant desde HttpContext.
/// Soporta configuración manual vía ITenantSetter para Workers en background.
/// </summary>
public class CurrentTenantContext : ICurrentTenantContext, ITenantSetter
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _manualTenantId;
    private bool? _manualIsSuperAdmin;
    private volatile HttpTenantSnapshot? _httpSnapshot;

    private sealed record HttpTenantSnapshot(Guid? TenantId, bool IsSuperAdmin);

    public CurrentTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void SetTenantId(Guid? tenantId)
    {
        _manualTenantId = tenantId;
        if (tenantId.HasValue)
        {
            _manualIsSuperAdmin = false;
        }
    }

    public void SetSuperAdmin(bool isSuperAdmin)
    {
        _manualIsSuperAdmin = isSuperAdmin;
    }

    public Guid? TenantId
    {
        get
        {
            if (_manualTenantId.HasValue) return _manualTenantId;

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                return ResolveHttpSnapshot(httpContext).TenantId;
            }

            return _httpSnapshot?.TenantId;
        }
    }

    public bool IsSuperAdmin
    {
        get
        {
            if (_manualIsSuperAdmin.HasValue) return _manualIsSuperAdmin.Value;

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                return ResolveHttpSnapshot(httpContext).IsSuperAdmin;
            }

            return _httpSnapshot?.IsSuperAdmin ?? false;
        }
    }

    private HttpTenantSnapshot ResolveHttpSnapshot(HttpContext httpContext)
    {
        var tenantId = ExtractTenantIdFromHttpContext(httpContext);
        bool isSuperAdmin = httpContext.User.IsInRole(PlatformRoles.SuperAdmin) && tenantId == null;
        var snapshot = new HttpTenantSnapshot(tenantId, isSuperAdmin);

        // Conservar en el scope la resolución autenticada para sobrevivir a ExecutionContext.SuppressFlow()
        // (p. ej. callbacks de HybridCache.GetOrCreateAsync donde IHttpContextAccessor.HttpContext es null).
        _httpSnapshot = httpContext.User.Identity?.IsAuthenticated == true ? snapshot : null;
        return snapshot;
    }

    private static Guid? ExtractTenantIdFromHttpContext(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue("TenantId", out var tenantIdObj) == true &&
            tenantIdObj is Guid tenantId)
        {
            return tenantId;
        }

        var tenantClaim = httpContext.User.FindFirst(c =>
            c.Type.Equals("tenantId", StringComparison.OrdinalIgnoreCase) ||
            c.Type.Equals("tenant_id", StringComparison.OrdinalIgnoreCase) ||
            c.Type.Equals("tid", StringComparison.OrdinalIgnoreCase))?.Value;

        if (!string.IsNullOrWhiteSpace(tenantClaim) && Guid.TryParse(tenantClaim, out var claimTenantId))
        {
            return claimTenantId;
        }

        return null;
    }

    public bool HasTenant => TenantId.HasValue;
}
