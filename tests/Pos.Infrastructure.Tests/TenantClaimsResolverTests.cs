using System.Security.Claims;
using Pos.Domain.Common;
using Pos.Infrastructure.Multitenancy;
using Xunit;

namespace Pos.Infrastructure.Tests;

public class TenantClaimsResolverTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ClaimsPrincipal AuthenticatedUser(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    // ── TenantId desde claim (tres variantes de nombre) ──────────────────────

    [Theory]
    [InlineData("tenantId")]
    [InlineData("tenant_id")]
    [InlineData("tid")]
    public void Resolve_ValidTenantClaim_ReturnsTenantId(string claimType)
    {
        var tenantId = Guid.NewGuid();
        var user = AuthenticatedUser(new Claim(claimType, tenantId.ToString()));

        var (resolvedTenant, isSuperAdmin) = TenantClaimsResolver.Resolve(user, null);

        Assert.Equal(tenantId, resolvedTenant);
        Assert.False(isSuperAdmin);
    }

    // ── Claim inválido / vacío → TenantId null ───────────────────────────────

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_InvalidTenantClaim_ReturnsTenantIdNull(string badValue)
    {
        var user = AuthenticatedUser(new Claim("tenantId", badValue));

        var (tenantId, _) = TenantClaimsResolver.Resolve(user, null);

        Assert.Null(tenantId);
    }

    [Fact]
    public void Resolve_EmptyGuidTenantClaim_ReturnsTenantIdNull()
    {
        var user = AuthenticatedUser(new Claim("tenantId", Guid.Empty.ToString()));

        var (tenantId, _) = TenantClaimsResolver.Resolve(user, null);

        Assert.Null(tenantId);
    }

    // ── SuperAdmin sin tenant ────────────────────────────────────────────────

    [Fact]
    public void Resolve_SuperAdminWithNoTenantClaim_ReturnsSuperAdminTrue()
    {
        var user = AuthenticatedUser(new Claim(ClaimTypes.Role, PlatformRoles.SuperAdmin));

        var (tenantId, isSuperAdmin) = TenantClaimsResolver.Resolve(user, null);

        Assert.Null(tenantId);
        Assert.True(isSuperAdmin);
    }

    // ── SuperAdmin con tenant → fail-closed: IsSuperAdmin = false ────────────

    [Fact]
    public void Resolve_SuperAdminWithTenantClaim_ReturnsSuperAdminFalse()
    {
        var user = AuthenticatedUser(
            new Claim(ClaimTypes.Role, PlatformRoles.SuperAdmin),
            new Claim("tenantId", Guid.NewGuid().ToString()));

        var (tenantId, isSuperAdmin) = TenantClaimsResolver.Resolve(user, null);

        Assert.NotNull(tenantId);
        Assert.False(isSuperAdmin);
    }

    // ── Items["TenantId"] tiene prioridad sobre claim ────────────────────────

    [Fact]
    public void Resolve_ItemsHasTenantId_UsesItemsOverClaim()
    {
        var itemsTenantId = Guid.NewGuid();
        var claimTenantId = Guid.NewGuid();
        var user = AuthenticatedUser(new Claim("tenantId", claimTenantId.ToString()));
        var items = new Dictionary<object, object?> { ["TenantId"] = itemsTenantId };

        var (tenantId, _) = TenantClaimsResolver.Resolve(user, items);

        Assert.Equal(itemsTenantId, tenantId);
    }

    [Fact]
    public void Resolve_ItemsHasEmptyGuid_FallsBackToClaim()
    {
        var claimTenantId = Guid.NewGuid();
        var user = AuthenticatedUser(new Claim("tenantId", claimTenantId.ToString()));
        var items = new Dictionary<object, object?> { ["TenantId"] = Guid.Empty };

        var (tenantId, _) = TenantClaimsResolver.Resolve(user, items);

        Assert.Equal(claimTenantId, tenantId);
    }

    [Fact]
    public void Resolve_ItemsIsNull_ReadsFromClaims()
    {
        var tenantId = Guid.NewGuid();
        var user = AuthenticatedUser(new Claim("tid", tenantId.ToString()));

        var (resolved, _) = TenantClaimsResolver.Resolve(user, null);

        Assert.Equal(tenantId, resolved);
    }

    // ── Sin claims ni items: todo nulo/false ─────────────────────────────────

    [Fact]
    public void Resolve_NoClaimsNoItems_ReturnsNullAndFalse()
    {
        var user = AuthenticatedUser();

        var (tenantId, isSuperAdmin) = TenantClaimsResolver.Resolve(user, null);

        Assert.Null(tenantId);
        Assert.False(isSuperAdmin);
    }
}

