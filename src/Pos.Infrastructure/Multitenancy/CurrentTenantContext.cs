using Microsoft.AspNetCore.Http;
using Pos.Application.Common.Interfaces;

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
            if (httpContext == null) return null;

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
    }

    public bool IsSuperAdmin
    {
        get
        {
            if (_manualIsSuperAdmin.HasValue) return _manualIsSuperAdmin.Value;

            var httpContext = _httpContextAccessor.HttpContext;
            return httpContext?.User.IsInRole("SuperAdmin") ?? false;
        }
    }

    public bool HasTenant => TenantId.HasValue;
}
