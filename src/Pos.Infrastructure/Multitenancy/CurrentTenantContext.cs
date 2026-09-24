using Microsoft.AspNetCore.Http;
using Pos.Application.Common.Interfaces;

namespace Pos.Infrastructure.Multitenancy;

public interface ITenantSetter
{
    void SetTenantId(Guid? tenantId);
}

/// <summary>
/// Implementación concreta de ICurrentTenantContext que obtiene el contexto del tenant desde HttpContext.
/// Soporta configuración manual vía ITenantSetter para Workers en background.
/// </summary>
public class CurrentTenantContext : ICurrentTenantContext, ITenantSetter
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _manualTenantId;

    public CurrentTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void SetTenantId(Guid? tenantId)
    {
        _manualTenantId = tenantId;
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
            var httpContext = _httpContextAccessor.HttpContext;
            return httpContext?.User.IsInRole("SuperAdmin") ?? false;
        }
    }

    public bool HasTenant => TenantId.HasValue;
}
