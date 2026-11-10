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
}

