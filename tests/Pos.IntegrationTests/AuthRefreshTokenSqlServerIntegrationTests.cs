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
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Interceptors;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

/// <summary>
/// Ciclo de vida completo del refresh token rotativo (F1.G) contra SQL Server real:
///   - login emite cookie HttpOnly SameSite=Strict acotada a /api/v1/auth,
///   - refresh rota el token y detecta reuso revocando la familia,
///   - logout revoca y borra la cookie de forma idempotente,
///   - /auth/me resuelve la identidad exacta desde los claims para SuperAdmin y TenantAdmin.
/// </summary>
[Collection("IntegrationTests")]
public class AuthRefreshTokenSqlServerIntegrationTests : IAsyncLifetime
{
    private const string SuperAdminPassword = "SuperRefreshPass123!";
    private const string CookieName = "nalunpos_rt";
    private const string CookiePath = "/api/v1/auth";

    private static readonly JsonSerializerOptions s_camelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly MsSqlTestFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;
    private int _ipCounter;

    public AuthRefreshTokenSqlServerIntegrationTests(MsSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", _fixture.ConnectionString);
                builder.UseSetting("SuperAdminSettings:Email", "superadmin_refresh@test.com");
                builder.UseSetting("SuperAdminSettings:Password", SuperAdminPassword);
                builder.UseSetting("SuperAdminSettings:FirstName", "Super");
                builder.UseSetting("SuperAdminSettings:LastName", "Refresh");
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
                        ["TenantStatus:TenantStatusCacheTtlSeconds"] = "1"
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

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    private HttpClient CreateClient(string? bearerToken = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        int ipNum = Interlocked.Increment(ref _ipCounter);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.240.{(ipNum >> 8) & 0xFF}.{ipNum & 0xFF}");
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return client;
    }

    private async Task<(string Email, string Password)> SeedAndLoginSuperAdminAsync(HttpClient client)
    {
        string unique = Guid.NewGuid().ToString("N")[..10];
        string email = $"sa_refresh_{unique}@platform.test";

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
                "Refresh",
                TimeProvider.System);
            dbSuper.Users.Add(superUser);
            await dbSuper.SaveChangesAsync();
        }

        return (email, SuperAdminPassword);
    }

    private static async Task<LoginResponse> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(s_camelCaseOptions);
        Assert.NotNull(payload);
        return payload;
    }

    private static string ReadRefreshCookie(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var setCookieValues),
            "La respuesta debe incluir la cabecera Set-Cookie con el refresh token.");
        string? setCookie = setCookieValues!.FirstOrDefault(v => v.StartsWith(CookieName + "=", StringComparison.Ordinal));
        Assert.NotNull(setCookie);

        string token = setCookie!.Split(';')[0][(CookieName.Length + 1)..];
        Assert.False(string.IsNullOrWhiteSpace(token), "La cookie debe transportar un token opaco no vacío.");

        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"path={CookiePath}", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=", setCookie, StringComparison.OrdinalIgnoreCase);
        return token;
    }

    private static void AttachRefreshCookie(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"{CookieName}={token}");
    }

    private async Task<(Guid TenantId, string AdminEmail, LoginResponse AdminLogin)> CreateActiveTenantAndLoginAdminAsync(
        HttpClient anonymousClient,
        string superAdminToken)
    {
        string unique = Guid.NewGuid().ToString("N")[..10];
        string adminEmail = $"tadmin_refresh_{unique}@empresa.test";
        string adminPassword = "TenantRefreshPass123!";

        using var superAdminClient = CreateClient(superAdminToken);
        var createResponse = await superAdminClient.PostAsJsonAsync(
            "/api/v1/platform/tenants",
            new CreateTenantRequest($"Empresa Refresh {unique}", $"TAX-REFRESH-{unique}", adminEmail, adminPassword));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Guid tenantId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var activateResponse = await superAdminClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(true));
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        LoginResponse adminLogin = await LoginAsync(anonymousClient, adminEmail, adminPassword);
        return (tenantId, adminEmail, adminLogin);
    }

    [Fact]
    public async Task Login_EmitsHttpOnlyStrictRefreshCookie_AndRefreshRotatesIt_WhileReuseRevokesFamily()
    {
        using var anonymousClient = CreateClient();
        var (email, password) = await SeedAndLoginSuperAdminAsync(anonymousClient);

        // 1. Login: emite cookie nalunpos_rt con atributos exigidos
        var loginResponse = await anonymousClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        string firstRefreshToken = ReadRefreshCookie(loginResponse);

        // 2. Refresh: 200 con LoginResponse y expiresAtUtc en UTC ('Z') + rotación de cookie
        using var refreshClient = CreateClient();
        AttachRefreshCookie(refreshClient, firstRefreshToken);
        var refreshResponse = await refreshClient.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        string rawRefreshJson = await refreshResponse.Content.ReadAsStringAsync();
        using (var document = JsonDocument.Parse(rawRefreshJson))
        {
            string expiresAtUtc = document.RootElement.GetProperty("expiresAtUtc").GetString()!;
            Assert.EndsWith("Z", expiresAtUtc, StringComparison.Ordinal);
        }

        var refreshedPayload = JsonSerializer.Deserialize<LoginResponse>(rawRefreshJson, s_camelCaseOptions);
        Assert.NotNull(refreshedPayload);
        Assert.Equal(PlatformRoles.SuperAdmin, refreshedPayload.Role);
        Assert.Null(refreshedPayload.TenantId);

        string secondRefreshToken = ReadRefreshCookie(refreshResponse);
        Assert.NotEqual(firstRefreshToken, secondRefreshToken);

        // 3. Reuso del token anterior: 401 ProblemDetails y revocación de toda la familia
        using var reuseClient = CreateClient();
        AttachRefreshCookie(reuseClient, firstRefreshToken);
        var reuseResponse = await reuseClient.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
        Assert.Equal("application/problem+json", reuseResponse.Content.Headers.ContentType?.MediaType);

        // 4. La cookie rotada también queda revocada por detección de reuso
        using var revokedFamilyClient = CreateClient();
        AttachRefreshCookie(revokedFamilyClient, secondRefreshToken);
        var revokedFamilyResponse = await revokedFamilyClient.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedFamilyResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken_DeletesCookie_Returns204_AndIsIdempotent()
    {
        using var anonymousClient = CreateClient();
        var (email, password) = await SeedAndLoginSuperAdminAsync(anonymousClient);

        var loginResponse = await anonymousClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        string refreshToken = ReadRefreshCookie(loginResponse);

        using var logoutClient = CreateClient();
        AttachRefreshCookie(logoutClient, refreshToken);
        var logoutResponse = await logoutClient.PostAsync("/api/v1/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        Assert.True(logoutResponse.Headers.TryGetValues("Set-Cookie", out var deleteCookieValues));
        string deleteCookie = deleteCookieValues!.Single(v => v.StartsWith(CookieName + "=", StringComparison.Ordinal));
        Assert.Contains($"path={CookiePath}", deleteCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", deleteCookie, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            deleteCookie.Contains("max-age=0", StringComparison.OrdinalIgnoreCase) ||
            deleteCookie.Contains("expires=thu, 01 jan 1970", StringComparison.OrdinalIgnoreCase),
            $"La cookie de logout debe expirar de inmediato: {deleteCookie}");

        // Refresh posterior con la cookie revocada -> 401
        using var refreshClient = CreateClient();
        AttachRefreshCookie(refreshClient, refreshToken);
        var refreshAfterLogout = await refreshClient.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);

        // Idempotencia: segundo logout sin cookie -> 204
        using var secondLogoutClient = CreateClient();
        var secondLogout = await secondLogoutClient.PostAsync("/api/v1/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, secondLogout.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidSuperAdminBearer_ReturnsExactIdentity_WithNullTenantId()
    {
        using var anonymousClient = CreateClient();
        var (email, password) = await SeedAndLoginSuperAdminAsync(anonymousClient);
        var loginPayload = await LoginAsync(anonymousClient, email, password);

        using var client = CreateClient(loginPayload.Token);
        var response = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var currentUser = await response.Content.ReadFromJsonAsync<CurrentUserPayload>(s_camelCaseOptions);
        Assert.NotNull(currentUser);
        Assert.Equal(loginPayload.UserId, currentUser.UserId);
        Assert.Equal(loginPayload.RoleId, currentUser.RoleId);
        Assert.Equal(PlatformRoles.SuperAdmin, currentUser.Role);
        Assert.Equal(loginPayload.Email, currentUser.Email);
        Assert.Equal(loginPayload.FullName, currentUser.FullName);
        Assert.Null(currentUser.TenantId);
    }

    [Fact]
    public async Task Me_WithValidTenantAdminBearer_ReturnsExactIdentity_WithTenantId()
    {
        using var anonymousClient = CreateClient();
        var (email, password) = await SeedAndLoginSuperAdminAsync(anonymousClient);
        var superAdminLogin = await LoginAsync(anonymousClient, email, password);

        var (tenantId, adminEmail, adminLogin) = await CreateActiveTenantAndLoginAdminAsync(
            anonymousClient,
            superAdminLogin.Token);

        Assert.Equal(tenantId, adminLogin.TenantId);

        using var client = CreateClient(adminLogin.Token);
        var response = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var currentUser = await response.Content.ReadFromJsonAsync<CurrentUserPayload>(s_camelCaseOptions);
        Assert.NotNull(currentUser);
        Assert.Equal(adminLogin.UserId, currentUser.UserId);
        Assert.Equal(adminLogin.RoleId, currentUser.RoleId);
        Assert.Equal(TenantRoles.TenantAdmin, currentUser.Role);
        Assert.Equal(adminEmail, currentUser.Email);
        Assert.Equal(tenantId, currentUser.TenantId);
    }

    /// <summary>
    /// Valida que si el tenant está suspendido, el refresh devuelve 403 Tenant.Suspended
    /// y el token original NO se consume (sigue activo, no se inserta fila nueva).
    /// </summary>
    [Fact]
    public async Task Refresh_TenantSuspended_Returns403_AndOriginalTokenRemainsActive()
    {
        using var anonymousClient = CreateClient();
        var (email, password) = await SeedAndLoginSuperAdminAsync(anonymousClient);
        var superAdminLogin = await LoginAsync(anonymousClient, email, password);

        var (tenantId, adminEmail, adminLogin) = await CreateActiveTenantAndLoginAdminAsync(
            anonymousClient,
            superAdminLogin.Token);

        var loginResponse = await anonymousClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(adminEmail, "TenantRefreshPass123!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        string refreshToken = ReadRefreshCookie(loginResponse);

        // Suspender el tenant
        using var superAdminClient = CreateClient(superAdminLogin.Token);
        var suspendResponse = await superAdminClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(false));
        Assert.Equal(HttpStatusCode.OK, suspendResponse.StatusCode);

        // Esperar a que expire la caché de estado de tenant (TTL 1s configurado en test)
        await Task.Delay(1500);

        // Refresh con tenant suspendido → 403
        using var refreshClient = CreateClient();
        AttachRefreshCookie(refreshClient, refreshToken);
        var refreshResponse = await refreshClient.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, refreshResponse.StatusCode);
        Assert.Equal("application/problem+json", refreshResponse.Content.Headers.ContentType?.MediaType);

        var problemJson = await refreshResponse.Content.ReadAsStringAsync();
        using (var doc = JsonDocument.Parse(problemJson))
        {
            var code = doc.RootElement.GetProperty("code").GetString();
            Assert.Equal(TenantAccessPolicy.SuspendedCode, code);
        }

        // El token original debe seguir activo: segundo refresh (antes de reactivar) también debe fallar con 403
        using var refreshClient2 = CreateClient();
        AttachRefreshCookie(refreshClient2, refreshToken);
        var refreshResponse2 = await refreshClient2.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, refreshResponse2.StatusCode);

        // Reactivar tenant
        var activateResponse = await superAdminClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(true));
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        // Esperar a que expire la caché de estado de tenant
        await Task.Delay(1500);

        // El MISMO token debe refrescar con éxito ahora
        using var refreshClient3 = CreateClient();
        AttachRefreshCookie(refreshClient3, refreshToken);
        var refreshResponse3 = await refreshClient3.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, refreshResponse3.StatusCode);

        var refreshedPayload = await refreshResponse3.Content.ReadFromJsonAsync<LoginResponse>(s_camelCaseOptions);
        Assert.NotNull(refreshedPayload);
        Assert.Equal(TenantRoles.TenantAdmin, refreshedPayload.Role);
        Assert.Equal(tenantId, refreshedPayload.TenantId);
    }

    /// <summary>
    /// Valida que si el usuario está inactivo, el refresh devuelve 401 Auth.UserInvalid
    /// y el token original NO se consume.
    /// </summary>
    [Fact]
    public async Task Refresh_UserInactive_Returns401_AndOriginalTokenRemainsActive()
    {
        using var anonymousClient = CreateClient();
        var (email, password) = await SeedAndLoginSuperAdminAsync(anonymousClient);
        var superAdminLogin = await LoginAsync(anonymousClient, email, password);

        var (tenantId, adminEmail, adminLogin) = await CreateActiveTenantAndLoginAdminAsync(
            anonymousClient,
            superAdminLogin.Token);

        var loginResponse = await anonymousClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(adminEmail, "TenantRefreshPass123!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        string refreshToken = ReadRefreshCookie(loginResponse);

        // Desactivar el usuario directamente en BD
        await using (var db = _fixture.CreateDbContext(tenantId: tenantId))
        {
            var emailVo = new Email(adminEmail);
            var user = await db.Users
                .IgnoreQueryFilters()
                .FirstAsync(u => u.Email == emailVo);
            user.Deactivate(TimeProvider.System);
            await db.SaveChangesAsync();
        }

        // Refresh con usuario inactivo → 401
        using var refreshClient = CreateClient();
        AttachRefreshCookie(refreshClient, refreshToken);
        var refreshResponse = await refreshClient.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
        Assert.Equal("application/problem+json", refreshResponse.Content.Headers.ContentType?.MediaType);

        var problemJson = await refreshResponse.Content.ReadAsStringAsync();
        using (var doc = JsonDocument.Parse(problemJson))
        {
            var code = doc.RootElement.GetProperty("code").GetString();
            Assert.Equal("Auth.UserInvalid", code);
        }

        // El token original debe seguir activo: segundo refresh también debe fallar con 401
        using var refreshClient2 = CreateClient();
        AttachRefreshCookie(refreshClient2, refreshToken);
        var refreshResponse2 = await refreshClient2.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse2.StatusCode);
    }

    /// <summary>
    /// Proyección local del contrato HTTP <c>CurrentUserResponse</c> para poder ejecutar
    /// la prueba contra la base antes de que exista el tipo en Pos.Application (TDD).
    /// </summary>
    private sealed record CurrentUserPayload(
        Guid UserId,
        Guid RoleId,
        string Role,
        string Email,
        string FullName,
        Guid? TenantId);
}
