using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.IntegrationTests.Fixtures;

public static class AuthenticatedClientFactory
{
    private static int _clientIpCounter;

    public static async Task<(string Token, User User)> CreateTokenAsync(
        CustomWebApplicationFactory factory,
        Guid? tenantId = null,
        IEnumerable<string>? permissions = null)
    {
        var config = factory.Services.GetRequiredService<IConfiguration>();
        var generator = new JwtTokenGenerator(config);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        Guid roleId;
        if (tenantId == null && permissions == null)
        {
            roleId = Role.SuperAdminRoleId;
        }
        else
        {
            Guid effectiveTenantId = tenantId ?? Guid.NewGuid();
            var role = Role.Create(
                effectiveTenantId,
                $"TestRole_{Guid.NewGuid():N}"[..16],
                "Test Role",
                permissions);

            db.Roles.Add(role);
            roleId = role.Id;
            tenantId = effectiveTenantId;
        }

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
