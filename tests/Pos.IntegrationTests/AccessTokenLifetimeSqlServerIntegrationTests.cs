using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pos.Api.Contracts.Requests;
using Pos.Application.Authentication.Commands.Login;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Interceptors;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

/// <summary>
/// Verifica contra SQL Server real que el claim <c>exp</c> del access token y el
/// <see cref="LoginResponse.ExpiresAtUtc"/> salen del mismo cálculo y que la duración
/// configurada (<c>JwtSettings:AccessTokenLifetimeMinutes</c>) gobierna ambos valores.
/// </summary>
[Collection("IntegrationTests")]
public class AccessTokenLifetimeSqlServerIntegrationTests
{
    private readonly MsSqlTestFixture _fixture;

    public AccessTokenLifetimeSqlServerIntegrationTests(MsSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Login_WithDefaultLifetime_ExpClaimAndExpiresAtUtc_ShouldMatchWithinOneSecond()
    {
        await using var factory = CreateFactory(accessTokenLifetimeMinutes: null);

        var payload = await LoginSuperAdminAsync(factory);

        AssertExpirationContract(payload, expectedLifetimeMinutes: 60);
    }

    [Fact]
    public async Task Login_WithConfiguredFiveMinutes_ExpClaimAndExpiresAtUtc_ShouldChangeTogether()
    {
        await using var factory = CreateFactory(accessTokenLifetimeMinutes: 5);

        var payload = await LoginSuperAdminAsync(factory);

        AssertExpirationContract(payload, expectedLifetimeMinutes: 5);
    }

    private static void AssertExpirationContract(LoginResponse payload, int expectedLifetimeMinutes)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(payload.Token);
        DateTime jwtExpUtc = jwt.ValidTo;

        Assert.Equal(DateTimeKind.Utc, jwtExpUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, payload.ExpiresAtUtc.Kind);

        // JwtSecurityTokenHandler trunca exp a segundos: tolerancia de 1 segundo.
        TimeSpan delta = (jwtExpUtc - payload.ExpiresAtUtc).Duration();
        Assert.True(
            delta <= TimeSpan.FromSeconds(1),
            $"El claim exp ({jwtExpUtc:O}) y ExpiresAtUtc ({payload.ExpiresAtUtc:O}) difieren en {delta.TotalMilliseconds} ms; " +
            "deben provenir del mismo cálculo.");

        TimeSpan configuredLifetime = payload.ExpiresAtUtc - DateTime.UtcNow;
        Assert.InRange(
            configuredLifetime.TotalMinutes,
            expectedLifetimeMinutes - 1,
            expectedLifetimeMinutes + 1);
    }

    private WebApplicationFactory<Program> CreateFactory(int? accessTokenLifetimeMinutes)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", _fixture.ConnectionString);
                builder.UseSetting("SuperAdminSettings:Email", "superadmin_lifetime@test.com");
                builder.UseSetting("SuperAdminSettings:Password", "SuperSecurePass123!");
                builder.UseSetting("SuperAdminSettings:FirstName", "Super");
                builder.UseSetting("SuperAdminSettings:LastName", "Admin");
                builder.UseSetting("JwtSettings:Secret", "SuperSecretEnterpriseJwtKey_LongEnoughFor256Bits_NalunPos2026!");
                builder.UseSetting("JwtSettings:Issuer", "NalunPosApi");
                builder.UseSetting("JwtSettings:Audience", "NalunPosClients");
                builder.UseSetting("CorsSettings:AllowedOrigins:0", "http://localhost:4200");

                if (accessTokenLifetimeMinutes.HasValue)
                {
                    builder.UseSetting(
                        "JwtSettings:AccessTokenLifetimeMinutes",
                        accessTokenLifetimeMinutes.Value.ToString(CultureInfo.InvariantCulture));
                }

                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = _fixture.ConnectionString,
                        ["RateLimiting:AuthPolicy:PermitLimit"] = "100",
                        ["RateLimiting:SensitiveOperationsPolicy:PermitLimit"] = "100",
                        ["RateLimiting:GlobalApiPolicy:PermitLimit"] = "200"
                    });
                });

                builder.ConfigureTestServices(services =>
                {
                    var outboxDescriptor = services.FirstOrDefault(d =>
                        d.ImplementationType == typeof(Pos.Api.BackgroundServices.OutboxProcessorBackgroundService));
                    if (outboxDescriptor != null) services.Remove(outboxDescriptor);

                    var reconciliationDescriptor = services.FirstOrDefault(d =>
                        d.ImplementationType == typeof(Pos.Api.BackgroundServices.InvoiceReconciliationBackgroundService));
                    if (reconciliationDescriptor != null) services.Remove(reconciliationDescriptor);

                    services.RemoveAll<DbContextOptions<PosDbContext>>();
                    services.RemoveAll<DbContextOptions>();
                    var efConfigs = services
                        .Where(d => d.ServiceType.IsGenericType &&
                                    d.ServiceType.GetGenericTypeDefinition().FullName == "Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration`1" &&
                                    d.ServiceType.GetGenericArguments()[0] == typeof(PosDbContext))
                        .ToList();
                    foreach (var d in efConfigs) services.Remove(d);

                    services.AddDbContext<PosDbContext>((provider, options) =>
                    {
                        options.UseSqlServer(_fixture.ConnectionString);
                        options.AddPosSaveChangesInterceptors(provider);
                    });
                });
            });
    }

    private async Task<LoginResponse> LoginSuperAdminAsync(WebApplicationFactory<Program> factory)
    {
        string unique = Guid.NewGuid().ToString("N")[..10];
        string superEmail = $"sa_lifetime_{unique}@platform.test";
        const string superPassword = "SuperLifetimePassword123!";

        await using (var dbSuper = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            using var scope = factory.Services.CreateScope();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var superRole = await dbSuper.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == Role.SuperAdminRoleId);
            if (superRole == null)
            {
                superRole = Role.CreatePlatformRole(
                    Role.SuperAdminRoleId,
                    PlatformScope.TenantId,
                    PlatformRoles.SuperAdmin,
                    "Administrador Global",
                    SuperAdminRoleCatalog.Permissions,
                    TimeProvider.System);
                dbSuper.Roles.Add(superRole);
            }
            else
            {
                superRole.SyncPermissions(SuperAdminRoleCatalog.Permissions);
            }

            var superUser = User.Create(
                new Email(superEmail),
                new PasswordHash(hasher.HashPassword(superPassword)),
                Role.SuperAdminRoleId,
                tenantId: null,
                "Super",
                "Lifetime",
                TimeProvider.System);
            dbSuper.Users.Add(superUser);
            await dbSuper.SaveChangesAsync();
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.231.0.1");

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(superEmail, superPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        return payload;
    }
}
