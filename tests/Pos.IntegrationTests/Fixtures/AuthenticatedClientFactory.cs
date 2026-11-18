using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.IntegrationTests.Fixtures;

/// <summary>
/// Fábrica de clientes HTTP autenticados para pruebas de integración.
/// </summary>
/// <remarks>
/// Siempre se crea y activa una entidad <see cref="Tenant"/> correspondiente en la base de datos de pruebas,
/// de modo que el <see cref="TenantStatusEnforcementMiddleware"/> encuentre el tenant en estado
/// <see cref="TenantStatus.Active"/> y permita el acceso. Cuando no se especifica <paramref name="tenantId"/>,
/// se genera uno nuevo.
/// </remarks>
public static class AuthenticatedClientFactory
{
    private static int _clientIpCounter;

    /// <summary>
    /// Genera un token JWT y su usuario correspondiente persistido en la base de datos de pruebas.
    /// </summary>
    /// <param name="factory">Fábrica de la aplicación web en pruebas.</param>
    /// <param name="tenantId">Identificador opcional del tenant. Si es null, se genera uno nuevo.</param>
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

        Guid effectiveTenantId = tenantId ?? Guid.NewGuid();

        // Crear y activar el Tenant para que el middleware de control de acceso lo encuentre en Active.
        var existingTenant = await db.Tenants.FindAsync(effectiveTenantId);
        if (existingTenant is null)
        {
            var tenant = Tenant.Create(
                $"Tenant Test {effectiveTenantId.ToString()[..8]}",
                $"TAX-{effectiveTenantId.ToString()[..8]}",
                TimeProvider.System);
            tenant.Activate(TimeProvider.System);
            db.Tenants.Add(tenant);
        }

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

        string token = generator.GenerateToken(user, role.Name);
        return (token, user);
    }

    /// <summary>
    /// Crea y configura un cliente HTTP autenticado con cabeceras de autorización Bearer e IP X-Forwarded-For simulada.
    /// </summary>
    /// <param name="factory">Fábrica de la aplicación web en pruebas.</param>
    /// <param name="tenantId">Identificador opcional del tenant. Si es null, se genera uno nuevo.</param>
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
