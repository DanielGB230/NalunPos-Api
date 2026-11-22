using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
using Pos.Domain.Authentication;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Interceptors;
using Pos.IntegrationTests.Fixtures;
using Pos.IntegrationTests.Support;
using Xunit;

namespace Pos.IntegrationTests;

/// <summary>
/// F1.G.7 (ADR 0034): cambiar el estado activo/inactivo de un usuario por el endpoint real
/// <c>PATCH /api/v1/users/{id}/status</c> revoca todas sus sesiones activas.
///   - desactivar invalida todos los refresh tokens del usuario (401 Auth.RefreshTokenInvalid),
///   - reactivar no reanuda el mismo token,
///   - el aislamiento multitenant se mantiene,
///   - el hueco de un token activo de un usuario inactivo se cierra al reactivar,
///   - cambiar el rol no revoca sesiones y el refresh refleja el rol nuevo.
/// </summary>
[Collection("IntegrationTests")]
public class UserStatusSessionRevocationSqlServerIntegrationTests : IAsyncLifetime
{
    private const string SuperAdminPassword = "SuperStatusPass123!";
    private const string TenantUserPassword = "TenantSessionPass123!";
    private const string CookieName = "nalunpos_rt";
    private const string CookiePath = "/api/v1/auth";

    private static readonly JsonSerializerOptions s_camelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly MsSqlTestFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;
    private int _ipCounter;

    public UserStatusSessionRevocationSqlServerIntegrationTests(MsSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = BuildFactory();

        // Fuerza la creación del servidor de pruebas antes del primer request.
        _ = _factory.CreateClient();

        return Task.CompletedTask;
    }

    private WebApplicationFactory<Program> BuildFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", _fixture.ConnectionString);
                builder.UseSetting("SuperAdminSettings:Email", "superadmin_status@test.com");
                builder.UseSetting("SuperAdminSettings:Password", SuperAdminPassword);
                builder.UseSetting("SuperAdminSettings:FirstName", "Super");
                builder.UseSetting("SuperAdminSettings:LastName", "Status");
                builder.UseSetting("JwtSettings:Secret", "SuperSecretEnterpriseJwtKey_LongEnoughFor256Bits_NalunPos2026!");
                builder.UseSetting("JwtSettings:Issuer", "NalunPosApi");
                builder.UseSetting("JwtSettings:Audience", "NalunPosClients");
                builder.UseSetting("CorsSettings:AllowedOrigins:0", "http://localhost:4200");

                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = _fixture.ConnectionString,
                        ["RateLimiting:AuthPolicy:PermitLimit"] = "500",
                        ["RateLimiting:SensitiveOperationsPolicy:PermitLimit"] = "500",
                        ["RateLimiting:GlobalApiPolicy:PermitLimit"] = "1000",
                        ["TenantStatus:TenantStatusCacheTtlSeconds"] = "1",
                        ["RefreshToken:RotationLeewaySeconds"] = "0"
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

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task DeactivateUser_RevokesAllActiveFamilies_AndRefreshReturns401InvalidWithClearedCookie()
    {
        using var anonymousClient = CreateClient();
        var (tenantId, _, adminLogin) = await ProvisionTenantAndLoginAdminAsync(anonymousClient);

        var (email, password) = await SeedTenantUserAsync(tenantId, TenantRoles.Cajero, TenantUserPassword);
        var first = await LoginWithRefreshAsync(anonymousClient, email, password);
        var second = await LoginWithRefreshAsync(anonymousClient, email, password);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        using var adminClient = CreateClient(adminLogin.Token);
        var deactivateResponse = await adminClient.PatchAsJsonAsync(
            $"/api/v1/users/{first.Payload.UserId}/status",
            new ChangeStatusRequest(false));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        // Ambas familias quedaron revocadas con el motivo de dominio.
        await AssertTokenRevokedAsync(first.RefreshToken, RefreshTokenRevocationReasons.UserDeactivated);
        await AssertTokenRevokedAsync(second.RefreshToken, RefreshTokenRevocationReasons.UserDeactivated);

        foreach (string token in new[] { first.RefreshToken, second.RefreshToken })
        {
            using var refreshClient = CreateRefreshClient(token);
            var refreshResponse = await refreshClient.PostAsync("/api/v1/auth/refresh", content: null);

            await AssertProblemCodeAsync(
                refreshResponse,
                HttpStatusCode.Unauthorized,
                "Auth.RefreshTokenInvalid");
            RefreshTokenCookieAssertions.AssertSetCookie(refreshResponse, RefreshCookieExpectation.Cleared);
        }
    }

    [Fact]
    public async Task ReactivateUser_DoesNotResumeTheSameRevokedRefreshToken()
    {
        using var anonymousClient = CreateClient();
        var (tenantId, _, adminLogin) = await ProvisionTenantAndLoginAdminAsync(anonymousClient);

        var (email, password) = await SeedTenantUserAsync(tenantId, TenantRoles.Cajero, TenantUserPassword);
        var session = await LoginWithRefreshAsync(anonymousClient, email, password);

        using var adminClient = CreateClient(adminLogin.Token);

        var deactivateResponse = await adminClient.PatchAsJsonAsync(
            $"/api/v1/users/{session.Payload.UserId}/status",
            new ChangeStatusRequest(false));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var activateResponse = await adminClient.PatchAsJsonAsync(
            $"/api/v1/users/{session.Payload.UserId}/status",
            new ChangeStatusRequest(true));
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        // El token revocado al desactivar no se reanuda: sigue revocado con su motivo original.
        await AssertTokenRevokedAsync(session.RefreshToken, RefreshTokenRevocationReasons.UserDeactivated);

        using var refreshClient = CreateRefreshClient(session.RefreshToken);
        var refreshResponse = await refreshClient.PostAsync("/api/v1/auth/refresh", content: null);
        await AssertProblemCodeAsync(refreshResponse, HttpStatusCode.Unauthorized, "Auth.RefreshTokenInvalid");
        RefreshTokenCookieAssertions.AssertSetCookie(refreshResponse, RefreshCookieExpectation.Cleared);
    }

    [Fact]
    public async Task DeactivateUser_DoesNotAffectOtherUsersSessions_InSameOrOtherTenant()
    {
        using var anonymousClient = CreateClient();
        var (tenantId, _, adminLogin) = await ProvisionTenantAndLoginAdminAsync(anonymousClient);
        var (otherTenantId, _, _) = await ProvisionTenantAndLoginAdminAsync(anonymousClient);

        var (targetEmail, targetPassword) = await SeedTenantUserAsync(tenantId, TenantRoles.Cajero, TenantUserPassword);
        var targetSession = await LoginWithRefreshAsync(anonymousClient, targetEmail, targetPassword);

        var (sameTenantEmail, sameTenantPassword) = await SeedTenantUserAsync(tenantId, TenantRoles.Supervisor, TenantUserPassword);
        var sameTenantSession = await LoginWithRefreshAsync(anonymousClient, sameTenantEmail, sameTenantPassword);

        var (otherTenantEmail, otherTenantPassword) = await SeedTenantUserAsync(otherTenantId, TenantRoles.Cajero, TenantUserPassword);
        var otherTenantSession = await LoginWithRefreshAsync(anonymousClient, otherTenantEmail, otherTenantPassword);

        using var adminClient = CreateClient(adminLogin.Token);
        var deactivateResponse = await adminClient.PatchAsJsonAsync(
            $"/api/v1/users/{targetSession.Payload.UserId}/status",
            new ChangeStatusRequest(false));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        await AssertRefreshSucceedsAsync(sameTenantSession.RefreshToken);
        await AssertRefreshSucceedsAsync(otherTenantSession.RefreshToken);
    }

    [Fact]
    public async Task ActivateUser_RevokesActiveTokenSeededForInactiveUser_WithUserReactivatedReason()
    {
        using var anonymousClient = CreateClient();
        var (tenantId, _, adminLogin) = await ProvisionTenantAndLoginAdminAsync(anonymousClient);

        var (email, password) = await SeedTenantUserAsync(tenantId, TenantRoles.Cajero, TenantUserPassword);
        var session = await LoginWithRefreshAsync(anonymousClient, email, password);

        // Simula una revocación fallida tras desactivar: usuario inactivo con token aún activo.
        await using (var db = _fixture.CreateDbContext(tenantId))
        {
            var emailValue = new Email(email);
            var user = await db.Users
                .IgnoreQueryFilters()
                .FirstAsync(u => u.Email == emailValue);
            user.Deactivate(TimeProvider.System);
            await db.SaveChangesAsync();
        }

        var beforeActivation = await ReadTokenStateAsync(session.RefreshToken);
        Assert.Null(beforeActivation.RevokedAtUtc);

        using var adminClient = CreateClient(adminLogin.Token);
        var activateResponse = await adminClient.PatchAsJsonAsync(
            $"/api/v1/users/{session.Payload.UserId}/status",
            new ChangeStatusRequest(true));
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        await AssertTokenRevokedAsync(session.RefreshToken, RefreshTokenRevocationReasons.UserReactivated);

        using var refreshClient = CreateRefreshClient(session.RefreshToken);
        var refreshResponse = await refreshClient.PostAsync("/api/v1/auth/refresh", content: null);
        await AssertProblemCodeAsync(refreshResponse, HttpStatusCode.Unauthorized, "Auth.RefreshTokenInvalid");
        RefreshTokenCookieAssertions.AssertSetCookie(refreshResponse, RefreshCookieExpectation.Cleared);
    }

    [Fact]
    public async Task UpdateUserRole_DoesNotRevokeSessions_AndRefreshCarriesTheNewRole()
    {
        using var anonymousClient = CreateClient();
        var (tenantId, _, adminLogin) = await ProvisionTenantAndLoginAdminAsync(anonymousClient);

        var (email, password) = await SeedTenantUserAsync(tenantId, TenantRoles.Cajero, TenantUserPassword);
        var session = await LoginWithRefreshAsync(anonymousClient, email, password);
        Guid supervisorRoleId = await GetRoleIdAsync(tenantId, TenantRoles.Supervisor);

        using var adminClient = CreateClient(adminLogin.Token);
        var roleResponse = await adminClient.PatchAsJsonAsync(
            $"/api/v1/users/{session.Payload.UserId}/role",
            new UpdateUserRoleRequest(supervisorRoleId));
        Assert.Equal(HttpStatusCode.OK, roleResponse.StatusCode);

        // Cambiar el rol no revoca la sesión vigente.
        var state = await ReadTokenStateAsync(session.RefreshToken);
        Assert.Null(state.RevokedAtUtc);

        using var refreshClient = CreateRefreshClient(session.RefreshToken);
        var refreshResponse = await refreshClient.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>(s_camelCaseOptions);
        Assert.NotNull(refreshed);
        Assert.Equal(TenantRoles.Supervisor, refreshed.Role);
        Assert.Equal(supervisorRoleId, refreshed.RoleId);
    }

    // ─── Helpers compartidos de sesión y aprovisionamiento ───────────────────

    private HttpClient CreateClient(string? bearerToken = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        int ipNum = Interlocked.Increment(ref _ipCounter);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.250.{(ipNum >> 8) & 0xFF}.{ipNum & 0xFF}");
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return client;
    }

    private HttpClient CreateRefreshClient(string refreshToken)
    {
        var client = CreateClient();
        AttachRefreshCookie(client, refreshToken);
        return client;
    }

    private async Task<(Guid TenantId, string AdminEmail, LoginResponse AdminLogin)> ProvisionTenantAndLoginAdminAsync(
        HttpClient anonymousClient)
    {
        var superAdminLogin = await EnsureSuperAdminAndLoginAsync(anonymousClient);
        string unique = Guid.NewGuid().ToString("N")[..10];
        string adminEmail = $"tadmin_status_{unique}@empresa.test";

        using var superAdminClient = CreateClient(superAdminLogin.Token);
        var createResponse = await superAdminClient.PostAsJsonAsync(
            "/api/v1/platform/tenants",
            new CreateTenantRequest($"Empresa Status {unique}", $"TAX-STATUS-{unique}", adminEmail, TenantUserPassword));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Guid tenantId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var activateResponse = await superAdminClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(true));
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        LoginResponse adminLogin = await LoginAsync(anonymousClient, adminEmail, TenantUserPassword);
        return (tenantId, adminEmail, adminLogin);
    }

    private async Task<LoginResponse> EnsureSuperAdminAndLoginAsync(HttpClient client)
    {
        string unique = Guid.NewGuid().ToString("N")[..10];
        string email = $"sa_status_{unique}@platform.test";

        await using (var dbSuper = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            using var scope = _factory.Services.CreateScope();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var superRole = await dbSuper.Roles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == Role.SuperAdminRoleId);
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
                new Email(email),
                new PasswordHash(hasher.HashPassword(SuperAdminPassword)),
                Role.SuperAdminRoleId,
                tenantId: null,
                "Super",
                "Status",
                TimeProvider.System);
            dbSuper.Users.Add(superUser);
            await dbSuper.SaveChangesAsync();
        }

        return await LoginAsync(client, email, SuperAdminPassword);
    }

    private async Task<(string Email, string Password)> SeedTenantUserAsync(
        Guid tenantId,
        string roleName,
        string password)
    {
        string email = $"{roleName.ToLowerInvariant()}_{Guid.NewGuid():N}@empresa.test";

        await using (var db = _fixture.CreateDbContext(tenantId))
        {
            using var scope = _factory.Services.CreateScope();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var role = await db.Roles.FirstAsync(r => r.TenantId == tenantId && r.Name == roleName);

            var user = User.Create(
                new Email(email),
                new PasswordHash(hasher.HashPassword(password)),
                role.Id,
                tenantId,
                "Session",
                roleName,
                TimeProvider.System);

            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        return (email, password);
    }

    private async Task<Guid> GetRoleIdAsync(Guid tenantId, string roleName)
    {
        await using var db = _fixture.CreateDbContext(tenantId);
        return await db.Roles
            .Where(r => r.TenantId == tenantId && r.Name == roleName)
            .Select(r => r.Id)
            .SingleAsync();
    }

    private static async Task<LoginResponse> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(s_camelCaseOptions);
        Assert.NotNull(payload);
        return payload;
    }

    private static async Task<(LoginResponse Payload, string RefreshToken)> LoginWithRefreshAsync(
        HttpClient client,
        string email,
        string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(s_camelCaseOptions);
        Assert.NotNull(payload);
        string refreshToken = ReadRefreshCookie(response);
        return (payload, refreshToken);
    }

    private static string ReadRefreshCookie(HttpResponseMessage response)
    {
        string setCookie = RefreshTokenCookieAssertions.AssertSetCookie(response, RefreshCookieExpectation.Issued)!;

        string token = setCookie.Split(';')[0][(CookieName.Length + 1)..];
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"path={CookiePath}", setCookie, StringComparison.OrdinalIgnoreCase);
        return token;
    }

    private static void AttachRefreshCookie(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"{CookieName}={token}");
    }

    private async Task AssertRefreshSucceedsAsync(string refreshToken)
    {
        using var refreshClient = CreateRefreshClient(refreshToken);
        var response = await refreshClient.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task AssertTokenRevokedAsync(string rawToken, string expectedReason)
    {
        var state = await ReadTokenStateAsync(rawToken);
        Assert.NotNull(state.RevokedAtUtc);
        Assert.Equal(expectedReason, state.RevokedReason);
    }

    private async Task<(DateTime? RevokedAtUtc, string? RevokedReason)> ReadTokenStateAsync(string rawToken)
    {
        string hash = HashOf(rawToken);
        await using var db = _fixture.CreateDbContext(isSuperAdmin: true);
        var row = await db.RefreshTokens.AsNoTracking().SingleAsync(r => r.TokenHash == hash);
        return (row.RevokedAtUtc, row.RevokedReason);
    }

    private static async Task AssertProblemCodeAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, problem.RootElement.GetProperty("code").GetString());
    }

    private static string HashOf(string rawToken)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
