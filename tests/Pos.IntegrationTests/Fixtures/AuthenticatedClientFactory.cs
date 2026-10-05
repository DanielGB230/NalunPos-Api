using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.IntegrationTests.Fixtures;

/// <summary>
/// Fábrica de clientes HTTP autenticados para pruebas de integración.
/// </summary>
public static class AuthenticatedClientFactory
{
    private static int _clientIpCounter;

    /// <summary>
    /// Genera un token JWT y su usuario correspondiente persistido en la base de datos de pruebas.
    /// </summary>
    /// <remarks>
    /// Cuando no se especifica un <paramref name="tenantId"/>, se genera un identificador único en memoria (<see cref="Guid.NewGuid"/>)
    /// que NO se persiste como entidad Tenant en la base de datos. Esto solo es adecuado para escenarios de prueba donde el handler 
    /// o endpoint no requiere acceder o validar datos persistidos de la entidad Tenant.
    /// </remarks>
    /// <param name="factory">Fábrica de la aplicación web en pruebas.</param>
    /// <param name="tenantId">Identificador opcional del tenant. Si es null, se genera un Guid temporal no persistido.</param>
    /// <param name="permissions">Lista opcional de permisos asignados al rol creado para el usuario de prueba.</param>
    /// <returns>Tupla con el token JWT generado y la entidad <see cref="User"/> persistida.</returns>
    public static async Task<(string Token, User User)> CreateTokenAsync(
        CustomWebApplicationFactory factory,
        Guid? tenantId = null,
        IEnumerable<string>? permissions = null)
    {
        var config = factory.Services.GetRequiredService<IConfiguration>();
        var generator = new JwtTokenGenerator(config);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        Guid effectiveTenantId = tenantId ?? Guid.NewGuid();
        var role = Role.Create(
            effectiveTenantId,
            $"TestRole_{Guid.NewGuid():N}"[..16],
            "Test Role",
            permissions);

        db.Roles.Add(role);
        Guid roleId = role.Id;
        tenantId = effectiveTenantId;

        var emailStr = $"test_{Guid.NewGuid():N}"[..16] + "@domain.com";
        var user = User.Create(
            new Email(emailStr),
            new PasswordHash("hashedpassword"),
            roleId,
            tenantId,
            "Test",
            "User");

        db.Users.Add(user);
        await db.SaveChangesAsync();

        string token = generator.GenerateToken(user);
        return (token, user);
    }

    /// <summary>
    /// Crea y configura un cliente HTTP autenticado con cabeceras de autorización Bearer e IP X-Forwarded-For simulada.
    /// </summary>
    /// <remarks>
    /// Cuando no se especifica un <paramref name="tenantId"/>, se genera un identificador único en memoria (<see cref="Guid.NewGuid"/>)
    /// que NO se persiste como entidad Tenant en la base de datos. Esto solo es adecuado para escenarios de prueba donde el handler 
    /// o endpoint no requiere acceder o validar datos persistidos de la entidad Tenant.
    /// </remarks>
    /// <param name="factory">Fábrica de la aplicación web en pruebas.</param>
    /// <param name="tenantId">Identificador opcional del tenant.</param>
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
