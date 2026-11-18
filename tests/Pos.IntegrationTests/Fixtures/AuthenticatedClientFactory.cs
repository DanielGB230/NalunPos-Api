using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Caching;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.IntegrationTests.Fixtures;

/// <summary>
/// Fábrica de clientes HTTP autenticados para pruebas de integración.
/// </summary>
/// <remarks>
/// Siempre se crea y activa una entidad <see cref="Tenant"/> correspondiente en la base de datos de pruebas,
/// de modo que el <see cref="TenantStatusEnforcementMiddleware"/> encuentre el tenant en estado
/// <see cref="TenantStatus.Active"/> y permita el acceso. El identificador real del tenant
/// (<c>tenant.Id</c>) es el que se usa para el rol, el usuario y el token, de modo que el tenant del token
/// siempre exista en la base de datos.
/// </remarks>
public static class AuthenticatedClientFactory
{
    private static int _clientIpCounter;

    /// <summary>
    /// Genera un token JWT y su usuario correspondiente persistido en la base de datos de pruebas.
    /// </summary>
    /// <param name="factory">Fábrica de la aplicación web en pruebas.</param>
    /// <param name="tenantId">
    /// Identificador de un tenant YA existente que se debe reutilizar. Si es <c>null</c>, la fábrica
    /// crea un tenant nuevo. Si el tenant indicado no existe, la operación falla de forma explícita
    /// (fail-fast) para no emitir un token cuyo tenant no exista en la base de datos.
    /// </param>
    /// <param name="permissions">Lista opcional de permisos asignados al rol creado para el usuario de prueba.</param>
    /// <returns>Tupla con el token JWT generado y la entidad <see cref="User"/> persistida.</returns>
    public static async Task<(string Token, User User)> CreateTokenAsync(
        CustomWebApplicationFactory factory,
        Guid? tenantId = null,
        IEnumerable<string>? permissions = null)
    {
        var config = factory.Services.GetRequiredService<IConfiguration>();
        var generator = new JwtTokenGenerator(config, TimeProvider.System);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        // 1. Crear PRIMERO el Tenant (o reutilizar el existente indicado por el llamador) y usar
        //    SIEMPRE tenant.Id como tenantId del rol, del usuario y del token. El middleware de
        //    control de acceso es fail-closed: un tenantId del token que no exista en BD produce 403.
        Tenant tenant = await ResolveTenantAsync(db, tenantId);

        Guid effectiveTenantId = tenant.Id;

        var role = Role.Create(
            effectiveTenantId,
            $"TestRole_{Guid.NewGuid():N}"[..16],
            "Test Role",
            permissions,
            TimeProvider.System);

        db.Roles.Add(role);
        Guid roleId = role.Id;

        var emailStr = $"test_{Guid.NewGuid():N}"[..16] + "@domain.com";
        var user = User.Create(
            new Email(emailStr),
            new PasswordHash("hashedpassword"),
            roleId,
            effectiveTenantId,
            "Test",
            "User",
            TimeProvider.System);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Invalidar caché de estado para forzar lectura fresca en el middleware
        var cache = factory.Services.GetRequiredService<HybridCache>();
        await cache.RemoveAsync(TenantCacheKeys.TenantStatusForTenant(effectiveTenantId));

        string token = generator.GenerateToken(user, role.Name);
        return (token, user);
    }

    /// <summary>
    /// Resuelve el tenant de pruebas: reutiliza el existente indicado por el llamador o crea uno
    /// nuevo ya activo. Devuelve siempre el tenant persistido con su identificador real.
    /// </summary>
    private static async Task<Tenant> ResolveTenantAsync(PosDbContext db, Guid? tenantId)
    {
        if (tenantId.HasValue)
        {
            var existingTenant = await db.Tenants.FindAsync(tenantId.Value);
            if (existingTenant is null)
            {
                throw new InvalidOperationException(
                    $"El tenant '{tenantId.Value}' no existe en la base de datos de pruebas. " +
                    "Cree y active el tenant antes de pasarlo a la fábrica de clientes autenticados.");
            }

            if (existingTenant.Status != TenantStatus.Active)
            {
                throw new InvalidOperationException(
                    $"El tenant '{existingTenant.Id}' no está activo (estado '{existingTenant.Status}'). " +
                    "El middleware de control de acceso denegaría la petición.");
            }

            return existingTenant;
        }

        string uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var tenant = Tenant.Create(
            $"Tenant Test {uniqueSuffix}",
            $"TAX-{uniqueSuffix}",
            TimeProvider.System);
        tenant.Activate(TimeProvider.System);
        db.Tenants.Add(tenant);
        return tenant;
    }

    /// <summary>
    /// Crea y configura un cliente HTTP autenticado con cabeceras de autorización Bearer e IP X-Forwarded-For simulada.
    /// </summary>
    /// <param name="factory">Fábrica de la aplicación web en pruebas.</param>
    /// <param name="tenantId">
    /// Identificador de un tenant YA existente que se debe reutilizar. Si es <c>null</c>, la fábrica
    /// crea un tenant nuevo ya activo.
    /// </param>
    /// <param name="permissions">Lista opcional de permisos para el rol de pruebas del usuario.</param>
    /// <returns>Tupla con el <see cref="HttpClient"/> configurado, la entidad <see cref="User"/> y el token JWT.</returns>
    public static async Task<(HttpClient Client, User User, string Token)> CreateAuthenticatedClientAsync(
        CustomWebApplicationFactory factory,
        Guid? tenantId = null,
        IEnumerable<string>? permissions = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        int ipCount = Interlocked.Increment(ref _clientIpCounter);
        string ip = $"10.200.{(ipCount >> 8) & 0xFF}.{ipCount & 0xFF}";
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);

        var (token, user) = await CreateTokenAsync(factory, tenantId, permissions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return (client, user, token);
    }
}
