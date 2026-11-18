using System.Globalization;
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
using Pos.Application.Common.Models;
using Pos.Application.Platform.Tenants.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Interceptors;
using Pos.Infrastructure.Persistence.Seed;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

[Collection("IntegrationTests")]
public class PlatformTenantProvisioningIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlTestFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;
    private const string SuperAdminEmail = "superadmin_provisioning@nalunpos.com";
    private const string SuperAdminPassword = "SuperPassword2026!Secured";
    private string _effectiveSuperAdminEmail = SuperAdminEmail;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public PlatformTenantProvisioningIntegrationTests(MsSqlTestFixture fixture)
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

        // Sembrar el SuperAdmin en SQL Server real utilizando el SuperAdminSeeder de infraestructura real
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

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    private HttpClient CreateClient(string? bearerToken = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.50.0.{Random.Shared.Next(1, 250)}");

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return client;
    }

    private async Task<string> LoginAsSuperAdminAsync()
    {
        using var client = CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            _effectiveSuperAdminEmail,
            SuperAdminPassword));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var authResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(s_jsonOptions);
        Assert.NotNull(authResult);
        Assert.NotNull(authResult.Token);
        Assert.Equal(PlatformRoles.SuperAdmin, authResult.Role);

        return authResult.Token;
    }

    private async Task<(Guid TenantId, string AdminEmail, string AdminPassword)> CreateTenantAsync(string superAdminToken)
    {
        using var client = CreateClient(superAdminToken);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        string name = $"Empresa_{suffix}";
        string docNumber = $"20{Random.Shared.Next(100000000, 999999999)}";
        string adminEmail = $"admin_{suffix}@tenantcorp.com";
        string adminPassword = "InitialTenantPass2026!Secured";

        var createReq = new CreateTenantRequest(name, docNumber, adminEmail, adminPassword);
        var response = await client.PostAsJsonAsync("/api/v1/platform/tenants", createReq);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var createdId = await response.Content.ReadFromJsonAsync<Guid>(s_jsonOptions);
        Assert.NotEqual(Guid.Empty, createdId);
        Assert.Contains(createdId.ToString(), response.Headers.Location.ToString());

        return (createdId, adminEmail, adminPassword);
    }

    [Fact]
    public async Task SuperAdmin_RealLogin_CanGetAndCreateTenants_AndInitialTenantAdminIsCreated()
    {
        // 1. Login real por HTTP como SuperAdmin
        string superAdminToken = await LoginAsSuperAdminAsync();

        // 2. GET /api/v1/platform/tenants -> 200 OK
        using var client = CreateClient(superAdminToken);
        var getResponse = await client.GetAsync("/api/v1/platform/tenants?pageNumber=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var pagedResult = await getResponse.Content.ReadFromJsonAsync<PagedResult<TenantDto>>(s_jsonOptions);
        Assert.NotNull(pagedResult);

        // 3. POST /api/v1/platform/tenants -> 201 Created (verificar Location y creación en BD)
        var (tenantId, adminEmail, _) = await CreateTenantAsync(superAdminToken);

        // 4. Verificación directa en base de datos real SQL Server
        using var dbContext = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true);
        var tenantInDb = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId);
        Assert.NotNull(tenantInDb);

        var adminUserInDb = await dbContext.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == adminEmail);
        Assert.NotNull(adminUserInDb);
        Assert.Equal(tenantId, adminUserInDb.TenantId);

        var adminRole = await dbContext.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == adminUserInDb.RoleId);
        Assert.NotNull(adminRole);
        Assert.Equal(TenantRoles.TenantAdmin, adminRole.Name);
    }

    [Fact]
    public async Task GetTenants_SerializesCreatedAtUtc_AsUtcInstantWithZSuffix()
    {
        // 1. SuperAdmin real (sembrado en SQL Server) y tenant recién aprovisionado
        string superAdminToken = await LoginAsSuperAdminAsync();
        var (tenantId, _, _) = await CreateTenantAsync(superAdminToken);

        // 2. GET /api/v1/platform/tenants -> 200 OK
        using var client = CreateClient(superAdminToken);
        var getResponse = await client.GetAsync("/api/v1/platform/tenants?pageNumber=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        // 3. Inspección del JSON crudo: el instante debe llevar sufijo 'Z' y leerse como Kind=Utc
        string payload = await getResponse.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        JsonElement tenant = document.RootElement
            .GetProperty("items")
            .EnumerateArray()
            .Single(element => element.GetProperty("id").GetGuid() == tenantId);

        string createdAtUtc = tenant.GetProperty("createdAtUtc").GetString()!;
        Assert.EndsWith("Z", createdAtUtc, StringComparison.Ordinal);

        DateTime parsed = DateTime.Parse(createdAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        Assert.Equal(DateTimeKind.Utc, parsed.Kind);
    }

    [Fact]
    public async Task TenantAdmin_CreatedDuringProvisioning_CanLogin_AndHasTenantAdminRoleAndTenantId()
    {
        // 1. SuperAdmin aprovisiona un nuevo Tenant y su TenantAdmin
        string superAdminToken = await LoginAsSuperAdminAsync();
        var (tenantId, adminEmail, adminPassword) = await CreateTenantAsync(superAdminToken);

        // 2. Activar el tenant antes de permitir login (solo Active puede autenticarse)
        using var saClient = CreateClient(superAdminToken);
        var activateResp = await saClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(true));
        Assert.Equal(HttpStatusCode.OK, activateResp.StatusCode);

        // 3. Login real por HTTP con el TenantAdmin recién creado
        using var client = CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            adminEmail,
            adminPassword));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var authResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(s_jsonOptions);

        Assert.NotNull(authResult);
        Assert.NotNull(authResult.Token);
        Assert.Equal(TenantRoles.TenantAdmin, authResult.Role);
        Assert.Equal(tenantId, authResult.TenantId);
        Assert.Equal(adminEmail, authResult.Email);
    }

    [Fact]
    public async Task TenantAdmin_CallingPlatformEndpoints_Returns403Forbidden_EnsuringMultiTenantIsolation()
    {
        // 1. SuperAdmin aprovisiona un nuevo Tenant y su TenantAdmin
        string superAdminToken = await LoginAsSuperAdminAsync();
        var (tenantId, adminEmail, adminPassword) = await CreateTenantAsync(superAdminToken);

        // 2. Activar el tenant antes de permitir login
        using var saClient = CreateClient(superAdminToken);
        var activateResp = await saClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(true));
        Assert.Equal(HttpStatusCode.OK, activateResp.StatusCode);

        // 3. Login como TenantAdmin para obtener su token JWT
        using var authClient = CreateClient();
        var loginResponse = await authClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            adminEmail,
            adminPassword));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var authResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(s_jsonOptions);
        Assert.NotNull(authResult);
        string tenantAdminToken = authResult.Token;

        // 4. AISLAMIENTO MULTI-TENANT: TenantAdmin llama GET /api/v1/platform/tenants -> 403 Forbidden
        using var platformClient = CreateClient(tenantAdminToken);
        var getResponse = await platformClient.GetAsync("/api/v1/platform/tenants");
        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);

        // 5. AISLAMIENTO MULTI-TENANT: TenantAdmin llama POST /api/v1/platform/tenants -> 403 Forbidden
        var createForbiddenReq = new CreateTenantRequest(
            "EmpresaIntrusa",
            "20999888777",
            "intruder@corp.com",
            "IntruderPass2026!");
        var postResponse = await platformClient.PostAsJsonAsync("/api/v1/platform/tenants", createForbiddenReq);
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);
    }
}
