using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Pos.Domain.Common;
using Pos.Infrastructure.Multitenancy;
using Xunit;

namespace Pos.Infrastructure.Tests;

public class CurrentTenantContextTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static HttpContextAccessor CreateAccessor(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        return new HttpContextAccessor { HttpContext = httpContext };
    }

    // ── Getters: resolución desde HttpContext (sin fijación manual) ──────────

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

    // ── Getters puros: sin efecto lateral ───────────────────────────────────

    [Fact]
    public void Getters_AreStateless_ReflectHttpContextOnEachRead()
    {
        // Primera configuración: usuario autenticado con tenant A
        var tenantA = Guid.NewGuid();
        var accessor = CreateAccessor(
            new Claim(ClaimTypes.Role, TenantRoles.TenantAdmin),
            new Claim("tenantId", tenantA.ToString()));
        var ctx = new CurrentTenantContext(accessor);

        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(tenantA, ctx.TenantId);
            Assert.False(ctx.IsSuperAdmin);
        }

        // Cambiar HttpContext a tenant B sin llamar ITenantSetter: refleja tenant B inmediatamente
        var tenantB = Guid.NewGuid();
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Role, TenantRoles.TenantAdmin),
                    new Claim("tenantId", tenantB.ToString())
                ],
                "TestAuth"))
        };

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(tenantB, ctx.TenantId);
            Assert.False(ctx.IsSuperAdmin);
        }

        // Reemplazar HttpContext con uno anónimo (sin claims): no debe retenerse estado anterior.
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()) // no autenticado
        };

        Assert.Null(ctx.TenantId);
        Assert.False(ctx.IsSuperAdmin);
    }

    [Fact]
    public void SetTenantId_ExplicitNull_OverridesHttpContextClaim()
    {
        var accessor = CreateAccessor(
            new Claim(ClaimTypes.Role, TenantRoles.TenantAdmin),
            new Claim("tenantId", Guid.NewGuid().ToString()));
        var ctx = new CurrentTenantContext(accessor);

        ((ITenantSetter)ctx).SetTenantId(null);

        Assert.Null(ctx.TenantId);
    }

    [Fact]
    public void Getters_WhenHttpContextBecomesNull_ReturnNullAndFalse()
    {
        var tenantId = Guid.NewGuid();
        var accessor = CreateAccessor(
            new Claim(ClaimTypes.Role, TenantRoles.TenantAdmin),
            new Claim("tenantId", tenantId.ToString()));
        var ctx = new CurrentTenantContext(accessor);

        // Materializamos una vez desde HttpContext
        Assert.Equal(tenantId, ctx.TenantId);

        // Quitamos el HttpContext: no debe haber snapshot mutable que retenga nada.
        accessor.HttpContext = null;

        Assert.Null(ctx.TenantId);
        Assert.False(ctx.IsSuperAdmin);
    }

    // ── Fijación manual (ITenantSetter) + SuppressFlow ───────────────────────

    [Fact]
    public async Task SetTenantId_AndSuppressFlow_TenantIdRetainedInsideSuppressedContext()
    {
        // Simula el flujo tras TenantResolutionMiddleware:
        //   1. Middleware llama SetTenantId(tenantId) ANTES de continuar al handler.
        //   2. HybridCache ejecuta callback bajo SuppressFlow (HttpContext = null en ese hilo).
        //   3. CurrentTenantContext devuelve el valor manual (_manualTenantId), no desde HttpContext.
        var tenantId = Guid.NewGuid();
        var accessor = CreateAccessor(
            new Claim(ClaimTypes.Role, TenantRoles.TenantAdmin),
            new Claim("tenantId", tenantId.ToString()));
        var ctx = new CurrentTenantContext(accessor);

        // Middleware fija explícitamente ANTES del SuppressFlow
        ((ITenantSetter)ctx).SetTenantId(tenantId);
        ((ITenantSetter)ctx).SetSuperAdmin(false);

        Guid? capturedTenant = null;
        bool? capturedSuperAdmin = null;
        bool? httpContextWasNull = null;

        Task task;
        using (ExecutionContext.SuppressFlow())
        {
            task = Task.Run(() =>
            {
                httpContextWasNull = accessor.HttpContext is null;
                capturedTenant = ctx.TenantId;
                capturedSuperAdmin = ctx.IsSuperAdmin;
            });
        }

        await task;

        Assert.True(httpContextWasNull, "HttpContext debe ser null dentro del SuppressFlow.");
        Assert.Equal(tenantId, capturedTenant);
        Assert.False(capturedSuperAdmin);
    }

    [Fact]
    public async Task SetSuperAdmin_AndSuppressFlow_SuperAdminRetainedInsideSuppressedContext()
    {
        // SuperAdmin: SetTenantId(null) + SetSuperAdmin(true) fijados por middleware.
        var accessor = CreateAccessor(new Claim(ClaimTypes.Role, PlatformRoles.SuperAdmin));
        var ctx = new CurrentTenantContext(accessor);

        ((ITenantSetter)ctx).SetTenantId(null);
        ((ITenantSetter)ctx).SetSuperAdmin(true);

        bool? capturedSuperAdmin = null;
        Guid? capturedTenant = null;

        Task task;
        using (ExecutionContext.SuppressFlow())
        {
            task = Task.Run(() =>
            {
                capturedTenant = ctx.TenantId;
                capturedSuperAdmin = ctx.IsSuperAdmin;
            });
        }

        await task;

        Assert.Null(capturedTenant);
        Assert.True(capturedSuperAdmin);
    }

    [Fact]
    public void SetTenantId_WithValue_AlsoSetsSuperAdminFalse()
    {
        var accessor = new HttpContextAccessor(); // sin HttpContext
        var ctx = new CurrentTenantContext(accessor);

        ((ITenantSetter)ctx).SetSuperAdmin(true); // primero marca superadmin
        ((ITenantSetter)ctx).SetTenantId(Guid.NewGuid()); // luego fija tenant → debe anular superadmin

        Assert.False(ctx.IsSuperAdmin);
        Assert.NotNull(ctx.TenantId);
    }

    // ── Dos requests consecutivos no comparten estado ────────────────────────

    [Fact]
    public void TwoDistinctInstances_HaveIndependentManualState()
    {
        // Cada request HTTP crea un nuevo scope DI → nueva instancia de CurrentTenantContext.
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var ctxA = new CurrentTenantContext(new HttpContextAccessor());
        var ctxB = new CurrentTenantContext(new HttpContextAccessor());

        ((ITenantSetter)ctxA).SetTenantId(tenantA);
        ((ITenantSetter)ctxB).SetTenantId(tenantB);

        Assert.Equal(tenantA, ctxA.TenantId);
        Assert.Equal(tenantB, ctxB.TenantId);
        Assert.NotEqual(ctxA.TenantId, ctxB.TenantId);
    }

    // ── Prueba de compatibilidad: usuario anónimo no retiene estado ──────────

    [Fact]
    public void UnauthenticatedHttpContext_DoesNotRetainSnapshot_WhenHttpContextBecomesNull()
    {
        var anonymousPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
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
