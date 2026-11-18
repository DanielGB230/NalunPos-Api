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
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

/// <summary>
/// Pruebas HTTP de extremo a extremo contra SQL Server real para el cambio de estado de Tenants.
/// El SuperAdmin se siembra con el seeder de infraestructura real y el TenantAdmin se obtiene
/// aprovisionando un tenant real vía el endpoint de plataforma.
/// </summary>
[Collection("IntegrationTests")]
public class TenantStatusSqlServerIntegrationTests : IAsyncLifetime
{
    private const string SuperAdminEmail = "superadmin_tenant_status@nalunpos.com";
    private const string SuperAdminPassword = "SuperPassword2026!Secured";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly MsSqlTestFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;
    private string _effectiveSuperAdminEmail = SuperAdminEmail;

    public TenantStatusSqlServerIntegrationTests(MsSqlTestFixture fixture)
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
                        ["RateLimiting:GlobalApiPolicy:PermitLimit"] = "500"
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

        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.70.0.{Random.Shared.Next(1, 250)}");

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
        return authResult.Token;
    }

    private async Task<(Guid TenantId, string AdminEmail, string AdminPassword)> CreateTenantAsync(string superAdminToken)
    {
        using var client = CreateClient(superAdminToken);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        string adminEmail = $"admin_{suffix}@tenantstatus.test";
        const string adminPassword = "InitialTenantPass2026!Secured";

        var response = await client.PostAsJsonAsync(
            "/api/v1/platform/tenants",
            new CreateTenantRequest(
                $"Empresa Status {suffix}",
                $"TAX-STATUS-{suffix}",
                adminEmail,
                adminPassword));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Guid tenantId = await response.Content.ReadFromJsonAsync<Guid>(s_jsonOptions);
        Assert.NotEqual(Guid.Empty, tenantId);

        return (tenantId, adminEmail, adminPassword);
    }

    private static Task<HttpResponseMessage> ChangeStatusAsync(HttpClient client, Guid tenantId, bool isActive)
    {
        return client.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(isActive));
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

    [Fact]
    public async Task SuperAdmin_CanActivateSuspendAndReactivateTenant_AgainstRealSqlServer()
    {
        // 1. SuperAdmin sembrado + tenant real en PendingProvisioning
        string superAdminToken = await LoginAsSuperAdminAsync();
        var (tenantId, _, _) = await CreateTenantAsync(superAdminToken);
        Assert.Equal(TenantStatus.PendingProvisioning, await GetTenantStatusAsync(tenantId));

        using var client = CreateClient(superAdminToken);

        // 2. PendingProvisioning -> Active  (isActive = true)
        var activateResponse = await ChangeStatusAsync(client, tenantId, true);
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        Assert.True(await activateResponse.Content.ReadFromJsonAsync<bool>(s_jsonOptions));
        Assert.Equal(TenantStatus.Active, await GetTenantStatusAsync(tenantId));

        // 3. Repetir Active -> 409 Tenant.AlreadyActive  (isActive = true)
        var alreadyActiveResponse = await ChangeStatusAsync(client, tenantId, true);
        Assert.Equal(HttpStatusCode.Conflict, alreadyActiveResponse.StatusCode);
        Assert.Equal("Tenant.AlreadyActive", await ReadProblemCodeAsync(alreadyActiveResponse));

        // 4. Active -> Suspended  (isActive = false)
        var suspendResponse = await ChangeStatusAsync(client, tenantId, false);
        Assert.Equal(HttpStatusCode.OK, suspendResponse.StatusCode);
        Assert.True(await suspendResponse.Content.ReadFromJsonAsync<bool>(s_jsonOptions));
        Assert.Equal(TenantStatus.Suspended, await GetTenantStatusAsync(tenantId));

        // 5. Repetir Suspended -> 409 Tenant.AlreadySuspended  (isActive = false)
        var alreadySuspendedResponse = await ChangeStatusAsync(client, tenantId, false);
        Assert.Equal(HttpStatusCode.Conflict, alreadySuspendedResponse.StatusCode);
        Assert.Equal("Tenant.AlreadySuspended", await ReadProblemCodeAsync(alreadySuspendedResponse));

        // 6. Suspended -> Active  (isActive = true)
        var reactivateResponse = await ChangeStatusAsync(client, tenantId, true);
        Assert.Equal(HttpStatusCode.OK, reactivateResponse.StatusCode);
        Assert.Equal(TenantStatus.Active, await GetTenantStatusAsync(tenantId));
    }

    [Fact]
    public async Task SuperAdmin_SuspendTenantInPendingProvisioning_Returns409InvalidTransitionAndKeepsState()
    {
        // Arrange
        string superAdminToken = await LoginAsSuperAdminAsync();
        var (tenantId, _, _) = await CreateTenantAsync(superAdminToken);

        using var client = CreateClient(superAdminToken);

        // Act: isActive = false sobre PendingProvisioning -> Suspend -> Tenant.InvalidTransition
        var response = await ChangeStatusAsync(client, tenantId, false);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Tenant.InvalidTransition", await ReadProblemCodeAsync(response));
        Assert.Equal(TenantStatus.PendingProvisioning, await GetTenantStatusAsync(tenantId));
    }

    [Fact]
    public async Task SuperAdmin_ChangeStatusForUnknownTenant_Returns404NotFound()
    {
        // Arrange
        string superAdminToken = await LoginAsSuperAdminAsync();
        using var client = CreateClient(superAdminToken);

        // Act: isActive = true sobre tenant inexistente
        var response = await ChangeStatusAsync(client, Guid.NewGuid(), true);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Tenant.NotFound", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task TenantAdmin_ChangeTenantStatus_Returns403ForbiddenAndKeepsState()
    {
        // Arrange: SuperAdmin aprovisiona un tenant real y su TenantAdmin
        string superAdminToken = await LoginAsSuperAdminAsync();
        var (tenantId, adminEmail, adminPassword) = await CreateTenantAsync(superAdminToken);

        // Activar el tenant antes de permitir login
        using var saClient = CreateClient(superAdminToken);
        var activateResp = await saClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(true));
        Assert.Equal(HttpStatusCode.OK, activateResp.StatusCode);

        using var authClient = CreateClient();
        var loginResponse = await authClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(adminEmail, adminPassword));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var authResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(s_jsonOptions);
        Assert.NotNull(authResult);
        Assert.Equal(TenantRoles.TenantAdmin, authResult.Role);

        using var tenantAdminClient = CreateClient(authResult.Token);

        // Act: isActive = true -> TenantAdmin recibe 403 (no tiene permiso de plataforma)
        var response = await ChangeStatusAsync(tenantAdminClient, tenantId, true);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(TenantStatus.Active, await GetTenantStatusAsync(tenantId));
    }
}
