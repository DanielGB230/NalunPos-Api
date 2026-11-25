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
        _factory = BuildFactory(rotationLeewaySeconds: 0);

        // Fuerza la creación del servidor de pruebas antes del primer request.
        _ = _factory.CreateClient();

        return Task.CompletedTask;
    }

    private WebApplicationFactory<Program> BuildFactory(int rotationLeewaySeconds)
    {
        return new WebApplicationFactory<Program>()
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
                        ["RefreshToken:RotationLeewaySeconds"] = rotationLeewaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
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

    private HttpClient CreateClient(string? bearerToken = null) => CreateClient(_factory, bearerToken);

    private HttpClient CreateClient(WebApplicationFactory<Program> factory, string? bearerToken)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
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
        string setCookie = RefreshTokenCookieAssertions.AssertSetCookie(response, RefreshCookieExpectation.Issued)!;

        string token = setCookie.Split(';')[0][(CookieName.Length + 1)..];
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

        using (var reuseProblem = JsonDocument.Parse(await reuseResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Auth.RefreshTokenInvalid", reuseProblem.RootElement.GetProperty("code").GetString());
        }

        // El 401 Auth.RefreshTokenInvalid es terminal: la cookie se limpia.
        RefreshTokenCookieAssertions.AssertSetCookie(reuseResponse, RefreshCookieExpectation.Cleared);

        // 4. La cookie rotada también queda revocada por detección de reuso
        using var revokedFamilyClient = CreateClient();
        AttachRefreshCookie(revokedFamilyClient, secondRefreshToken);
        var revokedFamilyResponse = await revokedFamilyClient.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedFamilyResponse.StatusCode);
        RefreshTokenCookieAssertions.AssertSetCookie(revokedFamilyResponse, RefreshCookieExpectation.Cleared);
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

        string deleteCookie = RefreshTokenCookieAssertions.AssertSetCookie(logoutResponse, RefreshCookieExpectation.Cleared)!;
        Assert.Contains($"path={CookiePath}", deleteCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", deleteCookie, StringComparison.OrdinalIgnoreCase);

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

        // Invalidar caché de estado de tenant de forma determinista
        await TenantStatusCacheControl.InvalidateAsync(_factory.Services, tenantId);

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

        // El 403 por tenant suspendido no consume el token: la cookie debe conservarse.
        RefreshTokenCookieAssertions.AssertSetCookie(refreshResponse, RefreshCookieExpectation.Absent);

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

        // Invalidar caché de estado de tenant de forma determinista
        await TenantStatusCacheControl.InvalidateAsync(_factory.Services, tenantId);

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

        // El 401 Auth.UserInvalid es recuperable (usuario inactivo): la cookie debe conservarse.
        RefreshTokenCookieAssertions.AssertSetCookie(refreshResponse, RefreshCookieExpectation.Absent);

        // El token original debe seguir activo: segundo refresh también debe fallar con 401 Auth.UserInvalid (NO Auth.RefreshTokenInvalid)
        using var refreshClient2 = CreateClient();
        AttachRefreshCookie(refreshClient2, refreshToken);
        var refreshResponse2 = await refreshClient2.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse2.StatusCode);
        var problemJson2 = await refreshResponse2.Content.ReadAsStringAsync();
        using (var doc2 = JsonDocument.Parse(problemJson2))
        {
            var code2 = doc2.RootElement.GetProperty("code").GetString();
            Assert.Equal("Auth.UserInvalid", code2);
            Assert.NotEqual("Auth.RefreshTokenInvalid", code2);
        }

        // Reactivar el usuario
        await using (var db = _fixture.CreateDbContext(tenantId: tenantId))
        {
            var emailVo = new Email(adminEmail);
            var user = await db.Users
                .IgnoreQueryFilters()
                .FirstAsync(u => u.Email == emailVo);
            user.Activate(TimeProvider.System);
            await db.SaveChangesAsync();
        }

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

    [Fact]
    public async Task Refresh_WithUnknownToken_Returns401RefreshTokenInvalid_AndClearsCookie()
    {
        using var client = CreateClient();
        AttachRefreshCookie(client, "unknown-refresh-token-value-0123456789");

        var response = await client.PostAsync("/api/v1/auth/refresh", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using (var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Auth.RefreshTokenInvalid", problem.RootElement.GetProperty("code").GetString());
        }

        // Sesión irrecuperable: la cookie se limpia con Max-Age=0.
        string clearingCookie = RefreshTokenCookieAssertions.AssertSetCookie(response, RefreshCookieExpectation.Cleared)!;
        Assert.Contains($"path={CookiePath}", clearingCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", clearingCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_ReuseWithinLeeway_Returns409WithoutCookie_AndFamilyRemainsUsable()
    {
        using var factory = BuildFactory(rotationLeewaySeconds: 30);
        using var anonymousClient = CreateClient(factory, bearerToken: null);
        var (email, password) = await SeedAndLoginSuperAdminAsync(anonymousClient);

        var loginResponse = await anonymousClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        string firstRefreshToken = ReadRefreshCookie(loginResponse);

        using var winnerClient = CreateClient(factory, bearerToken: null);
        AttachRefreshCookie(winnerClient, firstRefreshToken);
        var winnerResponse = await winnerClient.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, winnerResponse.StatusCode);
        string secondRefreshToken = ReadRefreshCookie(winnerResponse);

        // Reuso inmediato del token anterior dentro de la ventana: 409 sin cookie.
        using var reuseClient = CreateClient(factory, bearerToken: null);
        AttachRefreshCookie(reuseClient, firstRefreshToken);
        var reuseResponse = await reuseClient.PostAsync("/api/v1/auth/refresh", content: null);

        Assert.Equal(HttpStatusCode.Conflict, reuseResponse.StatusCode);
        Assert.Equal("application/problem+json", reuseResponse.Content.Headers.ContentType?.MediaType);

        RefreshTokenCookieAssertions.AssertSetCookie(reuseResponse, RefreshCookieExpectation.Absent);

        using (var problem = JsonDocument.Parse(await reuseResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Auth.RefreshConcurrent", problem.RootElement.GetProperty("code").GetString());
        }

        // La familia sigue viva: el token rotado por el ganador refresca con 200.
        using var familyClient = CreateClient(factory, bearerToken: null);
        AttachRefreshCookie(familyClient, secondRefreshToken);
        var familyResponse = await familyClient.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, familyResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_TwoParallelRequestsWithSameToken_OneSucceedsOneConflicts_AndWinnerTokenStillWorks()
    {
        using var factory = BuildFactory(rotationLeewaySeconds: 30);
        using var anonymousClient = CreateClient(factory, bearerToken: null);
        var (email, password) = await SeedAndLoginSuperAdminAsync(anonymousClient);

        var loginResponse = await anonymousClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        string refreshToken = ReadRefreshCookie(loginResponse);

        using var firstClient = CreateClient(factory, bearerToken: null);
        using var secondClient = CreateClient(factory, bearerToken: null);
        AttachRefreshCookie(firstClient, refreshToken);
        AttachRefreshCookie(secondClient, refreshToken);

        var responses = await Task.WhenAll(
            firstClient.PostAsync("/api/v1/auth/refresh", content: null),
            secondClient.PostAsync("/api/v1/auth/refresh", content: null));

        var successes = responses.Where(r => r.StatusCode == HttpStatusCode.OK).ToArray();
        var conflicts = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToArray();
        Assert.Single(successes);
        Assert.Single(conflicts);

        using (var problem = JsonDocument.Parse(await conflicts[0].Content.ReadAsStringAsync()))
        {
            Assert.Equal("Auth.RefreshConcurrent", problem.RootElement.GetProperty("code").GetString());
        }

        RefreshTokenCookieAssertions.AssertSetCookie(conflicts[0], RefreshCookieExpectation.Absent);

        string winnerToken = ReadRefreshCookie(successes[0]);
        using var winnerClient = CreateClient(factory, bearerToken: null);
        AttachRefreshCookie(winnerClient, winnerToken);
        var winnerRefresh = await winnerClient.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, winnerRefresh.StatusCode);

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task LoginAndSuccessiveRotations_KeepSameFamilyExpiresAtUtc_AndBackdatedFamilyReturns401WithClearedCookie()
    {
        using var anonymousClient = CreateClient();
        var (email, password) = await SeedAndLoginSuperAdminAsync(anonymousClient);

        var loginResponse = await anonymousClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        string currentToken = ReadRefreshCookie(loginResponse);

        var (familyId, familyExpiresAtUtc) = await ReadTokenFamilyAsync(currentToken);
        Assert.True(familyExpiresAtUtc > DateTime.UtcNow, "El login debe fijar una vigencia absoluta futura.");

        for (int rotation = 0; rotation < 3; rotation++)
        {
            using var refreshClient = CreateClient();
            AttachRefreshCookie(refreshClient, currentToken);
            var refreshResponse = await refreshClient.PostAsync("/api/v1/auth/refresh", content: null);
            Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

            currentToken = ReadRefreshCookie(refreshResponse);
            var (rotatedFamilyId, rotatedFamilyExpiresAtUtc) = await ReadTokenFamilyAsync(currentToken);
            Assert.Equal(familyId, rotatedFamilyId);
            Assert.Equal(familyExpiresAtUtc, rotatedFamilyExpiresAtUtc);
        }

        // Retroceder en BD la vigencia absoluta y la expiración del token actual.
        string currentHash = HashOf(currentToken);
        DateTime backdated = DateTime.UtcNow.AddSeconds(-5);
        await using (var db = _fixture.CreateDbContext(isSuperAdmin: true))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE RefreshTokens
                SET FamilyExpiresAtUtc = {backdated}, ExpiresAtUtc = {backdated}
                WHERE TokenHash = {currentHash}
                """);
        }

        using var expiredClient = CreateClient();
        AttachRefreshCookie(expiredClient, currentToken);
        var expiredResponse = await expiredClient.PostAsync("/api/v1/auth/refresh", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, expiredResponse.StatusCode);
        using (var problem = JsonDocument.Parse(await expiredResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Auth.RefreshTokenInvalid", problem.RootElement.GetProperty("code").GetString());
        }

        RefreshTokenCookieAssertions.AssertSetCookie(expiredResponse, RefreshCookieExpectation.Cleared);

        await using (var db = _fixture.CreateDbContext(isSuperAdmin: true))
        {
            var row = await db.RefreshTokens.AsNoTracking().SingleAsync(r => r.TokenHash == currentHash);
            Assert.NotNull(row.RevokedAtUtc);
            Assert.Equal(RefreshTokenRevocationReasons.Expired, row.RevokedReason);
        }
    }

    private async Task<(Guid FamilyId, DateTime FamilyExpiresAtUtc)> ReadTokenFamilyAsync(string rawToken)
    {
        string hash = HashOf(rawToken);
        await using var db = _fixture.CreateDbContext(isSuperAdmin: true);
        var row = await db.RefreshTokens.AsNoTracking().SingleAsync(r => r.TokenHash == hash);
        return (row.FamilyId, row.FamilyExpiresAtUtc);
    }

    private static string HashOf(string rawToken)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
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
