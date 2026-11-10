using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Pos.Domain.Common;
using Pos.Infrastructure.Multitenancy;
using Xunit;

namespace Pos.Infrastructure.Tests;

public class CurrentTenantContextTests
{
    private static HttpContextAccessor CreateAccessor(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        return new HttpContextAccessor { HttpContext = httpContext };
    }

    [Fact]
    public void IsSuperAdmin_WhenUserHasSuperAdminRoleAndNullTenantId_ReturnsTrue()
    {
        var accessor = CreateAccessor(new Claim(ClaimTypes.Role, PlatformRoles.SuperAdmin));
        var context = new CurrentTenantContext(accessor);

        Assert.Null(context.TenantId);
        Assert.True(context.IsSuperAdmin);
    }

    [Fact]
    public void IsSuperAdmin_WhenUserHasSuperAdminRoleAndNonNullTenantIdClaim_ReturnsFalse()
    {
        var accessor = CreateAccessor(
            new Claim(ClaimTypes.Role, PlatformRoles.SuperAdmin),
            new Claim("tenantId", Guid.NewGuid().ToString()));
        var context = new CurrentTenantContext(accessor);

        Assert.NotNull(context.TenantId);
        Assert.False(context.IsSuperAdmin);
    }

    [Fact]
    public void IsSuperAdmin_WhenUserHasTenantRoleAndNullTenantId_ReturnsFalse()
    {
        var accessor = CreateAccessor(new Claim(ClaimTypes.Role, TenantRoles.TenantAdmin));
        var context = new CurrentTenantContext(accessor);

        Assert.Null(context.TenantId);
        Assert.False(context.IsSuperAdmin);
    }

    [Fact]
    public async Task AuthenticatedHttpContext_RetainsResolvedTenantAndSuperAdmin_WhenHttpContextAccessorBecomesNullAcrossSuppressedExecutionFlow()
    {
        Guid tenantId = Guid.NewGuid();
        var tenantAccessor = CreateAccessor(
            new Claim(ClaimTypes.Role, TenantRoles.TenantAdmin),
            new Claim("tenantId", tenantId.ToString()));
        var tenantContext = new CurrentTenantContext(tenantAccessor);

        Assert.Equal(tenantId, tenantContext.TenantId);
        Assert.False(tenantContext.IsSuperAdmin);

        // Simular ExecutionContext.SuppressFlow() (como HybridCache.GetOrCreateAsync), donde AsyncLocal pierde HttpContext en el hilo delegado
        Guid? capturedTenantInSuppressedFlow = null;
        bool? capturedSuperAdminInSuppressedFlow = null;
        bool? httpContextWasNullInsideSuppressedFlow = null;

        Task task;
        using (ExecutionContext.SuppressFlow())
        {
            task = Task.Run(() =>
            {
                httpContextWasNullInsideSuppressedFlow = tenantAccessor.HttpContext is null;
                capturedTenantInSuppressedFlow = tenantContext.TenantId;
                capturedSuperAdminInSuppressedFlow = tenantContext.IsSuperAdmin;
            });
        }

        await task;
        Assert.True(httpContextWasNullInsideSuppressedFlow);
        Assert.Equal(tenantId, capturedTenantInSuppressedFlow);
        Assert.False(capturedSuperAdminInSuppressedFlow);

        var superAccessor = CreateAccessor(new Claim(ClaimTypes.Role, PlatformRoles.SuperAdmin));
        var superContext = new CurrentTenantContext(superAccessor);
        Assert.Null(superContext.TenantId);
        Assert.True(superContext.IsSuperAdmin);

        superAccessor.HttpContext = null;
        Assert.Null(superContext.TenantId);
        Assert.True(superContext.IsSuperAdmin);
    }

    [Fact]
    public void UnauthenticatedHttpContext_DoesNotRetainSnapshot_WhenHttpContextBecomesNull()
    {
        var anonymousPrincipal = new System.Security.Claims.ClaimsPrincipal(new ClaimsIdentity());
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = anonymousPrincipal,
                Items = { ["TenantId"] = Guid.NewGuid() }
            }
        };

        var context = new CurrentTenantContext(accessor);
        _ = context.TenantId;
        _ = context.IsSuperAdmin;

        accessor.HttpContext = null;

        Assert.Null(context.TenantId);
        Assert.False(context.IsSuperAdmin);
    }
}

