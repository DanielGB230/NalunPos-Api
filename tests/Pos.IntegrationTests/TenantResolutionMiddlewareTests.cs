using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Api.Middleware;
using Pos.Domain.Common;
using Pos.Infrastructure.Multitenancy;
using Xunit;

namespace Pos.IntegrationTests;

/// <summary>
/// Pruebas unitarias del <see cref="TenantResolutionMiddleware"/> usando dobles de prueba
/// sin infraestructura HTTP real (sin <see cref="WebApplicationFactory"/>).
/// </summary>
public class TenantResolutionMiddlewareTests
{
    // ── Doble de prueba para ITenantSetter ───────────────────────────────────

    private sealed class CapturingTenantSetter : ITenantSetter
    {
        public bool SetTenantIdCalled { get; private set; }
        public bool SetSuperAdminCalled { get; private set; }
        public Guid? CapturedTenantId { get; private set; } = Guid.Empty; // sentinel distinto de null
        public bool CapturedSuperAdmin { get; private set; }

        public void SetTenantId(Guid? tenantId)
        {
            SetTenantIdCalled = true;
            CapturedTenantId = tenantId;
        }

        public void SetSuperAdmin(bool isSuperAdmin)
        {
            SetSuperAdminCalled = true;
            CapturedSuperAdmin = isSuperAdmin;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static (TenantResolutionMiddleware middleware, CapturingTenantSetter setter) CreateMiddleware()
    {
        var setter = new CapturingTenantSetter();
        var middleware = new TenantResolutionMiddleware(
            _ => Task.CompletedTask,
            NullLogger<TenantResolutionMiddleware>.Instance);
        return (middleware, setter);
    }

    private static DefaultHttpContext CreateHttpContext(bool isAuthenticated, params Claim[] claims)
    {
        var identity = isAuthenticated
            ? new ClaimsIdentity(claims, "TestAuth")
            : new ClaimsIdentity();
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
    }

    // ── Tenant autenticado: SetTenantId con el Guid correcto ─────────────────

    [Fact]
    public async Task InvokeAsync_TenantUser_CallsSetTenantIdWithCorrectGuid()
    {
        var (middleware, setter) = CreateMiddleware();
        var tenantId = Guid.NewGuid();
        var ctx = CreateHttpContext(true,
            new Claim(ClaimTypes.Role, TenantRoles.TenantAdmin),
            new Claim("tenantId", tenantId.ToString()));

        await middleware.InvokeAsync(ctx, setter);

        Assert.True(setter.SetTenantIdCalled);
        Assert.Equal(tenantId, setter.CapturedTenantId);
        Assert.True(setter.SetSuperAdminCalled);
        Assert.False(setter.CapturedSuperAdmin);
    }

    [Fact]
    public async Task InvokeAsync_TenantUser_SetsItemsTenantId()
    {
        var (middleware, setter) = CreateMiddleware();
        var tenantId = Guid.NewGuid();
        var ctx = CreateHttpContext(true,
            new Claim(ClaimTypes.Role, TenantRoles.TenantAdmin),
            new Claim("tenantId", tenantId.ToString()));

        await middleware.InvokeAsync(ctx, setter);

        Assert.True(ctx.Items.TryGetValue("TenantId", out var stored));
        Assert.Equal(tenantId, (Guid)stored!);
    }

    // ── SuperAdmin: SetTenantId(null) + SetSuperAdmin(true) ──────────────────

    [Fact]
    public async Task InvokeAsync_SuperAdmin_CallsSetTenantIdNullAndSetSuperAdminTrue()
    {
        var (middleware, setter) = CreateMiddleware();
        var ctx = CreateHttpContext(true,
            new Claim(ClaimTypes.Role, PlatformRoles.SuperAdmin));

        await middleware.InvokeAsync(ctx, setter);

        Assert.True(setter.SetTenantIdCalled);
        Assert.Null(setter.CapturedTenantId);
        Assert.True(setter.SetSuperAdminCalled);
        Assert.True(setter.CapturedSuperAdmin);
    }

    // ── Anónimo: NO debe llamar a SetTenantId ni SetSuperAdmin ───────────────

    [Fact]
    public async Task InvokeAsync_AnonymousUser_DoesNotCallTenantSetter()
    {
        var (middleware, setter) = CreateMiddleware();
        var ctx = CreateHttpContext(false);

        await middleware.InvokeAsync(ctx, setter);

        Assert.False(setter.SetTenantIdCalled);
        Assert.False(setter.SetSuperAdminCalled);
    }

    [Fact]
    public async Task InvokeAsync_AnonymousUser_DoesNotSetItemsTenantId()
    {
        var (middleware, setter) = CreateMiddleware();
        var ctx = CreateHttpContext(false);

        await middleware.InvokeAsync(ctx, setter);

        Assert.False(ctx.Items.ContainsKey("TenantId"));
    }

    // ── Claim con variantes de nombre ────────────────────────────────────────

    [Theory]
    [InlineData("tenantId")]
    [InlineData("tenant_id")]
    [InlineData("tid")]
    public async Task InvokeAsync_TenantUserWithClaimVariant_ResolvesCorrectly(string claimType)
    {
        var (middleware, setter) = CreateMiddleware();
        var tenantId = Guid.NewGuid();
        var ctx = CreateHttpContext(true, new Claim(claimType, tenantId.ToString()));

        await middleware.InvokeAsync(ctx, setter);

        Assert.Equal(tenantId, setter.CapturedTenantId);
    }

    // ── Integración Middleware + CurrentTenantContext real + SuppressFlow ────

    [Fact]
    public async Task InvokeAsync_WithRealCurrentTenantContext_PreservesTenantAndSuperAdminAcrossSuppressedFlow()
    {
        var tenantId = Guid.NewGuid();
        var ctx = CreateHttpContext(true,
            new Claim(ClaimTypes.Role, TenantRoles.TenantAdmin),
            new Claim("tenantId", tenantId.ToString()));
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var tenantContext = new CurrentTenantContext(accessor);

        Guid? tenantInsideNextSuppressed = null;
        bool? superAdminInsideNextSuppressed = null;
        bool? httpContextNullInsideSuppressed = null;

        var middleware = new TenantResolutionMiddleware(
            async _ =>
            {
                Task t;
                using (ExecutionContext.SuppressFlow())
                {
                    t = Task.Run(() =>
                    {
                        httpContextNullInsideSuppressed = accessor.HttpContext is null;
                        tenantInsideNextSuppressed = tenantContext.TenantId;
                        superAdminInsideNextSuppressed = tenantContext.IsSuperAdmin;
                    });
                }
                await t;
            },
            NullLogger<TenantResolutionMiddleware>.Instance);

        await middleware.InvokeAsync(ctx, tenantContext);

        Assert.True(httpContextNullInsideSuppressed);
        Assert.Equal(tenantId, tenantInsideNextSuppressed);
        Assert.False(superAdminInsideNextSuppressed);
    }
}
