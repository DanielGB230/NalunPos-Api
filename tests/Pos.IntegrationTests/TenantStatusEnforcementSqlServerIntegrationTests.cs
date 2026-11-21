using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Caching.Hybrid;
using Pos.Api.Contracts.Requests;
using Pos.Application.Authentication.Commands.Login;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Interceptors;
using Pos.Infrastructure.Persistence.Seed;
using Pos.Infrastructure.Caching;
using Pos.IntegrationTests.Fixtures;
using Pos.IntegrationTests.Support;
using Xunit;

namespace Pos.IntegrationTests;

/// <summary>
/// Pruebas HTTP de extremo a extremo contra SQL Server real para el control de acceso
/// por estado de tenant (F1.G.2).
/// Valida que solo un tenant en estado Active pueda autenticar y operar, tanto en el login
/// como en cada petición autenticada posterior.
/// </summary>
[Collection("IntegrationTests")]
public class TenantStatusEnforcementSqlServerIntegrationTests : IAsyncLifetime
{
    private const string SuperAdminEmail = "superadmin_enforcement@nalunpos.com";
    private const string SuperAdminPassword = "SuperPassword2026!Secured";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly MsSqlTestFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;
    private string _effectiveSuperAdminEmail = SuperAdminEmail;

    public TenantStatusEnforcementSqlServerIntegrationTests(MsSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", _fixture.ConnectionString);
                builder.UseSetting("SuperAdminSettings:Email", SuperAdminEmail);
                builder.UseSetting("SuperAdminSettings:Password", SuperAdminPassword);
                builder.UseSetting("SuperAdminSettings:FirstName", "Super");
                builder.UseSetting("SuperAdminSettings:LastName", "Admin");
                builder.UseSetting("JwtSettings:Secret", "SuperSecretEnterpriseJwtKey_LongEnoughFor256Bits_NalunPos2026!");
                builder.UseSetting("JwtSettings:Issuer", "NalunPosApi");
                builder.UseSetting("JwtSettings:Audience", "NalunPosClients");
                builder.UseSetting("CorsSettings:AllowedOrigins:0", "http://localhost:4200");

                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = _fixture.ConnectionString,
                        ["RateLimiting:AuthPolicy:PermitLimit"] = "200",
                        ["RateLimiting:SensitiveOperationsPolicy:PermitLimit"] = "200",
                        ["RateLimiting:GlobalApiPolicy:PermitLimit"] = "500",
                        // TTL muy bajo (1 s) para evitar esperas y permitir pruebas deterministas de propagación
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

        await EnsureSuperAdminAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    private async Task EnsureSuperAdminAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<SuperAdminSeeder>();
        await seeder.SeedAsync();

        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var superAdmin = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.RoleId == Role.SuperAdminRoleId);
        _effectiveSuperAdminEmail = superAdmin.Email.Value;
        superAdmin.UpdatePassword(new PasswordHash(hasher.HashPassword(SuperAdminPassword)), TimeProvider.System);
        await db.SaveChangesAsync();
    }

    private HttpClient CreateClient(string? bearerToken = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.80.0.{Random.Shared.Next(1, 250)}");

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return client;
    }

    private async Task<string> LoginAsSuperAdminAsync()
    {
        await EnsureSuperAdminAsync();

        using var client = CreateClient();
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(_effectiveSuperAdminEmail, SuperAdminPassword));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var authResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(s_jsonOptions);
        Assert.NotNull(authResult);
        Assert.Equal(PlatformRoles.SuperAdmin, authResult.Role);
        return authResult.Token!;
    }

    private async Task<(Guid TenantId, string AdminEmail, string AdminPassword)> CreateTenantAsync(string superAdminToken)
    {
        using var client = CreateClient(superAdminToken);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        string adminEmail = $"admin_enf_{suffix}@enforcement.test";
        const string adminPassword = "InitialEnfPass2026!Secured";

        var response = await client.PostAsJsonAsync(
            "/api/v1/platform/tenants",
            new CreateTenantRequest(
                $"Empresa Enforcement {suffix}",
                $"TAX-ENF-{suffix}",
                adminEmail,
                adminPassword));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Guid tenantId = await response.Content.ReadFromJsonAsync<Guid>(s_jsonOptions);
        Assert.NotEqual(Guid.Empty, tenantId);

        return (tenantId, adminEmail, adminPassword);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password)
    {
        return client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, password));
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        string json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("code", out var codeElement)
            ? codeElement.GetString()
            : null;
    }

    private async Task<TenantStatus> GetTenantStatusAsync(Guid tenantId)
    {
        await using var db = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true);
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
        return tenant.Status;
    }

    /// <summary>
    /// (a) Tenant recién creado (PendingProvisioning): login del TenantAdmin → 403 con code Tenant.NotActive.
    /// </summary>
    [Fact]
    public async Task TenantPendingProvisioning_Login_Returns403NotActive()
    {
        // Arrange
        string superAdminToken = await LoginAsSuperAdminAsync();
        var (tenantId, adminEmail, adminPassword) = await CreateTenantAsync(superAdminToken);
        Assert.Equal(TenantStatus.PendingProvisioning, await GetTenantStatusAsync(tenantId));

        // Act
        using var client = CreateClient();
        var response = await LoginAsync(client, adminEmail, adminPassword);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(TenantAccessPolicy.NotActiveCode, await ReadProblemCodeAsync(response));
        Assert.Equal(TenantStatus.PendingProvisioning, await GetTenantStatusAsync(tenantId));
    }

    /// <summary>
    /// (g) Petición autenticada con tenantId inexistente → 403 code Tenant.NotActive (fail-closed).
    /// </summary>
    [Fact]
    public async Task InexistentTenant_TenantRequest_Returns403NotActive()
    {
        // Arrange: SuperAdmin crea y activa un tenant, obtiene token de TenantAdmin,
        // luego elimina el tenant de BD e invalida la caché de estado.
        string superAdminToken = await LoginAsSuperAdminAsync();
        var (tenantId, adminEmail, adminPassword) = await CreateTenantAsync(superAdminToken);

        using var saClient = CreateClient(superAdminToken);
        var activateResp = await saClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(true));
        Assert.Equal(HttpStatusCode.OK, activateResp.StatusCode);

        using var anonClient = CreateClient();
        var loginResp = await LoginAsync(anonClient, adminEmail, adminPassword);
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var loginPayload = await loginResp.Content.ReadFromJsonAsync<LoginResponse>(s_jsonOptions);
        Assert.NotNull(loginPayload);
        string token = loginPayload!.Token!;

        // Eliminar el tenant de BD (tenantId ahora inexistente)
        await using var db = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true);
        var tenantToDelete = await db.Tenants.SingleAsync(t => t.Id == tenantId);
        db.Tenants.Remove(tenantToDelete);
        await db.SaveChangesAsync();

        // Invalidar caché de estado para forzar lectura fresca (null = inexistente)
        var cache = _factory.Services.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>();
        await cache.RemoveAsync(TenantCacheKeys.TenantStatusForTenant(tenantId));

        // Act: petición autenticada con tenantId inexistente → 403 Tenant.NotActive
        using var tenantClient = CreateClient(token);
        var response = await tenantClient.GetAsync("/api/v1/users");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(TenantAccessPolicy.NotActiveCode, await ReadProblemCodeAsync(response));
    }

    /// <summary>
    /// (b) Tras activar (PATCH status isActive=true): login 200 y petición de tenant con ese token → 200.
    /// </summary>
    [Fact]
    public async Task TenantActive_LoginAndTenantRequest_Returns200()
    {
        // Arrange
        string superAdminToken = await LoginAsSuperAdminAsync();
        var (tenantId, adminEmail, adminPassword) = await CreateTenantAsync(superAdminToken);

        using var superAdminClient = CreateClient(superAdminToken);
        var activateResponse = await superAdminClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(true));
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        Assert.Equal(TenantStatus.Active, await GetTenantStatusAsync(tenantId));

        // Act: login del TenantAdmin
        using var client = CreateClient();
        var loginResponse = await LoginAsync(client, adminEmail, adminPassword);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(s_jsonOptions);
        Assert.NotNull(loginPayload);
        Assert.NotNull(loginPayload.Token);

        // Act: petición autenticada de tenant (usar endpoint GET /api/v1/users como operación de tenant)
        using var tenantClient = CreateClient(loginPayload.Token);
        var tenantResponse = await tenantClient.GetAsync("/api/v1/users");
        Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
    }

    /// <summary>
    /// (c) Tras suspender: el MISMO token → 403 code Tenant.Suspended y un login nuevo → 403 Tenant.Suspended.
    /// </summary>
    [Fact]
    public async Task TenantSuspended_ExistingTokenAndNewLogin_Returns403Suspended()
    {
        // Arrange: tenant activo con token válido
        string superAdminToken = await LoginAsSuperAdminAsync();
        var (tenantId, adminEmail, adminPassword) = await CreateTenantAsync(superAdminToken);

        using var superAdminClient = CreateClient(superAdminToken);
        await superAdminClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(true));
        Assert.Equal(TenantStatus.Active, await GetTenantStatusAsync(tenantId));

        using var client = CreateClient();
        var loginResponse = await LoginAsync(client, adminEmail, adminPassword);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(s_jsonOptions);
        string token = loginPayload!.Token!;

        // Act: suspender el tenant
        var suspendResponse = await superAdminClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(false));
        Assert.Equal(HttpStatusCode.OK, suspendResponse.StatusCode);
        Assert.Equal(TenantStatus.Suspended, await GetTenantStatusAsync(tenantId));

        // Invalidar caché de estado para forzar lectura fresca (suspendido → denegar)
        var cacheSuspend = _factory.Services.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>();
        await cacheSuspend.RemoveAsync(TenantCacheKeys.TenantStatusForTenant(tenantId));

        // Act (c1): el MISMO token → 403 Tenant.Suspended
        using var tenantClient = CreateClient(token);
        var response1 = await tenantClient.GetAsync("/api/v1/users");
        Assert.Equal(HttpStatusCode.Forbidden, response1.StatusCode);
        Assert.Equal(TenantAccessPolicy.SuspendedCode, await ReadProblemCodeAsync(response1));

        // Act (c2): login nuevo → 403 Tenant.Suspended
        using var anonymousClient = CreateClient();
        var loginResponse2 = await LoginAsync(anonymousClient, adminEmail, adminPassword);
        Assert.Equal(HttpStatusCode.Forbidden, loginResponse2.StatusCode);
        Assert.Equal(TenantAccessPolicy.SuspendedCode, await ReadProblemCodeAsync(loginResponse2));

        // El login fallido no debe borrar la cookie de una sesión válida en otra pestaña.
        RefreshTokenCookieAssertions.AssertSetCookie(loginResponse2, RefreshCookieExpectation.Absent);
    }

    /// <summary>
    /// (d) Reactivar → vuelve a 200.
    /// </summary>
    [Fact]
    public async Task TenantReactivated_LoginAndRequest_Returns200()
    {
        // Arrange: tenant suspendido
        string superAdminToken = await LoginAsSuperAdminAsync();
        var (tenantId, adminEmail, adminPassword) = await CreateTenantAsync(superAdminToken);

        using var superAdminClient = CreateClient(superAdminToken);
        await superAdminClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(true));
        await superAdminClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(false));
        Assert.Equal(TenantStatus.Suspended, await GetTenantStatusAsync(tenantId));

        // Reactivar
        var reactivateResponse = await superAdminClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(true));
        Assert.Equal(HttpStatusCode.OK, reactivateResponse.StatusCode);
        Assert.Equal(TenantStatus.Active, await GetTenantStatusAsync(tenantId));

        // Invalidar caché de estado para forzar lectura fresca (activo → permitir)
        var cacheReactive = _factory.Services.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>();
        await cacheReactive.RemoveAsync(TenantCacheKeys.TenantStatusForTenant(tenantId));

        // Act: login y petición → 200
        using var client = CreateClient();
        var loginResponse = await LoginAsync(client, adminEmail, adminPassword);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(s_jsonOptions);
        Assert.NotNull(loginPayload);

        using var tenantClient = CreateClient(loginPayload.Token);
        var tenantResponse = await tenantClient.GetAsync("/api/v1/users");
        Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
    }

    /// <summary>
    /// (e) Credenciales inválidas contra un tenant suspendido siguen dando el mismo 401 genérico
    /// (no revelar el estado sin credenciales válidas).
    /// </summary>
    [Fact]
    public async Task InvalidCredentialsAgainstSuspendedTenant_Returns401Generic()
    {
        // Arrange: tenant suspendido
        string superAdminToken = await LoginAsSuperAdminAsync();
        var (tenantId, adminEmail, adminPassword) = await CreateTenantAsync(superAdminToken);

        using var superAdminClient = CreateClient(superAdminToken);
        await superAdminClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(true));
        await superAdminClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(false));
        Assert.Equal(TenantStatus.Suspended, await GetTenantStatusAsync(tenantId));

        // Act: credenciales inválidas
        using var client = CreateClient();
        var response = await LoginAsync(client, adminEmail, "WrongPassword2026!Secured");

        // Assert: mismo 401 genérico, no revela el estado del tenant
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(DomainError.Unauthorized("Auth.InvalidCredentials", "Credenciales de acceso inválidas.").Code,
            await ReadProblemCodeAsync(response));

        // Un login fallido no debe borrar la cookie de una sesión válida en otra pestaña.
        RefreshTokenCookieAssertions.AssertSetCookie(response, RefreshCookieExpectation.Absent);
    }

    /// <summary>
    /// (f) El SuperAdmin no se ve afectado por el estado de ningún tenant.
    /// </summary>
    [Fact]
    public async Task SuperAdmin_AlwaysCanLogin_RegardlessOfTenantState()
    {
        // Arrange: crear un tenant y suspenderlo
        string superAdminToken = await LoginAsSuperAdminAsync();
        var (tenantId, _, _) = await CreateTenantAsync(superAdminToken);

        using var superAdminClient = CreateClient(superAdminToken);
        await superAdminClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(true));
        await superAdminClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(false));
        Assert.Equal(TenantStatus.Suspended, await GetTenantStatusAsync(tenantId));

        // Act: el SuperAdmin puede seguir haciendo login
        using var client = CreateClient();
        var response = await LoginAsync(client, _effectiveSuperAdminEmail, SuperAdminPassword);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(s_jsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(PlatformRoles.SuperAdmin, payload.Role);
    }
}
