using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
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
using Pos.Application.Roles;
using Pos.Application.Roles.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Interceptors;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

[Collection("IntegrationTests")]
public class AuthLoginSqlServerIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlTestFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;
    private int _ipCounter;

    public AuthLoginSqlServerIntegrationTests(MsSqlTestFixture fixture)
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
                builder.UseSetting("SuperAdminSettings:Email", "superadmin_sqlauth@test.com");
                builder.UseSetting("SuperAdminSettings:Password", "SuperSecurePass123!");
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
            AllowAutoRedirect = false
        });

        int ipNum = Interlocked.Increment(ref _ipCounter);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.220.{(ipNum >> 8) & 0xFF}.{ipNum & 0xFF}");
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return client;
    }

    private async Task<LoginResponse> CreateAndLoginSuperAdminAsync()
    {
        string unique = Guid.NewGuid().ToString("N")[..10];
        string superEmail = $"sa_sql_{unique}@platform.test";
        const string superPassword = "SuperSqlPassword123!";

        await using (var dbSuper = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            using var scope = _factory.Services.CreateScope();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var superRole = await dbSuper.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == Role.SuperAdminRoleId);
            if (superRole == null)
            {
                superRole = Role.CreatePlatformRole(
                    Role.SuperAdminRoleId,
                    PlatformScope.TenantId,
                    PlatformRoles.SuperAdmin,
                    "Administrador Global",
                    [Permissions.Tenants.Create, Permissions.Tenants.View],
                    TimeProvider.System);
                dbSuper.Roles.Add(superRole);
            }
            else
            {
                superRole.AddPermission(Permissions.Tenants.Create);
                superRole.AddPermission(Permissions.Tenants.View);
            }

            var superUser = User.Create(
                new Email(superEmail),
                new PasswordHash(hasher.HashPassword(superPassword)),
                Role.SuperAdminRoleId,
                tenantId: null,
                "Super",
                "SqlAdmin",
                TimeProvider.System);
            dbSuper.Users.Add(superUser);
            await dbSuper.SaveChangesAsync();
        }

        using var anonymousClient = CreateClient();
        var saLoginResponse = await anonymousClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(superEmail, superPassword));

        Assert.Equal(HttpStatusCode.OK, saLoginResponse.StatusCode);
        var saPayload = await saLoginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(saPayload);
        return saPayload;
    }

    [Fact]
    public async Task Login_AgainstSqlServer_SuperAdminAndAllFourTenantRoles_ShouldReturnCanonicalRoleAndJwtClaim()
    {
        // Confirmar que se está usando el scope real de SQL Server (sin No-Op ni InMemory)
        using (var scope = _factory.Services.CreateScope())
        {
            var sessionScope = scope.ServiceProvider.GetRequiredService<IAuthenticationSessionScope>();
            Assert.IsType<SqlServerAuthenticationSessionScope>(sessionScope);
        }

        // 1. Login SuperAdmin vía HTTP contra SQL Server
        var saPayload = await CreateAndLoginSuperAdminAsync();
        Assert.Equal(PlatformRoles.SuperAdmin, saPayload.Role);
        Assert.Null(saPayload.TenantId);
        Assert.Equal(Role.SuperAdminRoleId, saPayload.RoleId);
        AssertJwtRoleClaim(saPayload.Token, PlatformRoles.SuperAdmin);

        // 2. Aprovisionar Tenant real vía endpoint POST /api/v1/platform/tenants
        string unique = Guid.NewGuid().ToString("N")[..10];
        string tenantAdminEmail = $"admin_sql_{unique}@empresa.test";
        const string tenantPassword = "TenantPassword123!";

        using var superAdminClient = CreateClient(saPayload.Token);
        var createTenantResp = await superAdminClient.PostAsJsonAsync(
            "/api/v1/platform/tenants",
            new CreateTenantRequest($"Empresa SQL {unique}", $"TAX-{unique}", tenantAdminEmail, tenantPassword));

        Assert.Equal(HttpStatusCode.Created, createTenantResp.StatusCode);
        Guid createdTenantId = await createTenantResp.Content.ReadFromJsonAsync<Guid>();
        Assert.NotEqual(Guid.Empty, createdTenantId);

        // 3. Crear usuarios adicionales para Supervisor, InventoryManager y Cajero en el tenant recién aprovisionado
        Dictionary<string, string> roleEmails = new()
        {
            [TenantRoles.TenantAdmin] = tenantAdminEmail
        };

        await using (var tenantDb = _fixture.CreateDbContext(createdTenantId))
        {
            using var scope = _factory.Services.CreateScope();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var hashed = hasher.HashPassword(tenantPassword);
            var tenantRoles = await tenantDb.Roles.ToListAsync();

            foreach (var roleName in new[] { TenantRoles.Supervisor, TenantRoles.InventoryManager, TenantRoles.Cajero })
            {
                var roleEntity = Assert.Single(tenantRoles, r => r.Name == roleName);
                string email = $"{roleName.ToLowerInvariant()}_{unique}@empresa.test";
                var tenantUser = User.Create(
                    new Email(email),
                    new PasswordHash(hashed),
                    roleEntity.Id,
                    createdTenantId,
                    "Operador",
                    roleName,
                    TimeProvider.System);
                tenantDb.Users.Add(tenantUser);
                roleEmails[roleName] = email;
            }

            await tenantDb.SaveChangesAsync();
        }

        // 4. Act & Assert Login de los 4 roles canónicos del tenant vía HTTP contra SQL Server + RLS
        using var anonymousClient = CreateClient();
        foreach (var (expectedRole, email) in roleEmails)
        {
            var loginResp = await anonymousClient.PostAsJsonAsync(
                "/api/v1/auth/login",
                new LoginRequest(email, tenantPassword));

            Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
            var loginPayload = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(loginPayload);
            Assert.Equal(expectedRole, loginPayload.Role);
            Assert.Equal(createdTenantId, loginPayload.TenantId);
            AssertJwtRoleClaim(loginPayload.Token, expectedRole);
        }
    }

    [Fact]
    public async Task AuthUserLookup_AgainstSqlServer_ShouldRestorePreviousSessionContextAndBalancedConnectionState()
    {
        Guid tenantAId = Guid.NewGuid();
        Guid tenantBId = Guid.NewGuid();
        string emailB = $"tenantb_ctx_{Guid.NewGuid():N}@tenant.test";

        // Sembrar Tenant B con rol y usuario en SQL Server
        await using (var dbB = _fixture.CreateDbContext(tenantBId))
        {
            var roleB = Role.CreateSystemRole(tenantBId, TenantRoles.Cajero, "Cajero B", [Permissions.Sales.Create], TimeProvider.System);
            var userB = User.Create(new Email(emailB), new PasswordHash("HASH"), roleB.Id, tenantBId, "Usuario", "B", TimeProvider.System);
            dbB.Roles.Add(roleB);
            dbB.Users.Add(userB);
            await dbB.SaveChangesAsync();
        }

        // Caso A: Conexión abierta con TenantId = tenantAId e IsSuperAdmin = 0
        await using (var dbA = _fixture.CreateDbContext(tenantAId, isSuperAdmin: false))
        {
            await dbA.Database.OpenConnectionAsync();
            Assert.Equal(ConnectionState.Open, dbA.Database.GetDbConnection().State);

            var lookup = new AuthUserLookup(dbA, new SqlServerAuthenticationSessionScope(dbA));
            var authUser = await lookup.FindByEmailAsync(emailB);

            Assert.NotNull(authUser);
            Assert.Equal(TenantRoles.Cajero, authUser.Role);
            Assert.Equal(ConnectionState.Open, dbA.Database.GetDbConnection().State);

            var (allowGlobal, currentTenantId, isSuperAdmin) = await ReadSessionContextAsync(dbA);
            Assert.Equal(0, allowGlobal);
            Assert.Equal(tenantAId, currentTenantId);
            Assert.Equal(0, isSuperAdmin);
        }

        // Caso B: Conexión abierta con TenantId = NULL e IsSuperAdmin = 1
        await using (var dbSuper = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            await dbSuper.Database.OpenConnectionAsync();
            Assert.Equal(ConnectionState.Open, dbSuper.Database.GetDbConnection().State);

            var lookup = new AuthUserLookup(dbSuper, new SqlServerAuthenticationSessionScope(dbSuper));
            var authUser = await lookup.FindByEmailAsync(emailB);

            Assert.NotNull(authUser);
            Assert.Equal(TenantRoles.Cajero, authUser.Role);
            Assert.Equal(ConnectionState.Open, dbSuper.Database.GetDbConnection().State);

            var (allowGlobal, currentTenantId, isSuperAdmin) = await ReadSessionContextAsync(dbSuper);
            Assert.Equal(0, allowGlobal);
            Assert.Null(currentTenantId);
            Assert.Equal(1, isSuperAdmin);
        }

        // Caso C: Conexión cerrada al entrar debe quedar cerrada al salir
        await using (var dbClosed = _fixture.CreateDbContext(tenantAId, isSuperAdmin: false))
        {
            Assert.Equal(ConnectionState.Closed, dbClosed.Database.GetDbConnection().State);

            var lookup = new AuthUserLookup(dbClosed, new SqlServerAuthenticationSessionScope(dbClosed));
            var authUser = await lookup.FindByEmailAsync(emailB);
            Assert.NotNull(authUser);
            Assert.Equal(ConnectionState.Closed, dbClosed.Database.GetDbConnection().State);

            bool exists = await lookup.ExistsByEmailAsync(emailB);
            Assert.True(exists);
            Assert.Equal(ConnectionState.Closed, dbClosed.Database.GetDbConnection().State);
        }
    }

    [Fact]
    public async Task Login_AgainstSqlServer_WhenTenantRoleNamedSuperAdminInsertedInDb_ShouldReturn401Unauthorized()
    {
        Guid tenantId = Guid.NewGuid();
        Guid maliciousRoleId = Guid.NewGuid();
        string email = $"spoof_sql_{Guid.NewGuid():N}@tenant.test";
        const string password = "ValidPassword123!";

        // Insertar por SQL directo una fila en Roles de tenant con Name = PlatformRoles.SuperAdmin saltándose el dominio
        await using (var dbSuper = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            await dbSuper.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO [dbo].[Roles] (Id, TenantId, Name, Description, PermissionsJson) VALUES ({maliciousRoleId}, {tenantId}, {PlatformRoles.SuperAdmin}, {"Spoofed role"}, {"[]"})");

            using var scope = _factory.Services.CreateScope();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var user = User.Create(
                new Email(email),
                new PasswordHash(hasher.HashPassword(password)),
                maliciousRoleId,
                tenantId,
                "Malicious",
                "Actor",
                TimeProvider.System);
            dbSuper.Users.Add(user);
            await dbSuper.SaveChangesAsync();
        }

        // Act: intentar autenticarse con credenciales válidas sobre el rol espurio "SuperAdmin"
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        // Assert: defensa en profundidad falla cerrado devolviendo 401
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RolesEndpoint_AgainstSqlServer_WhenTenantAdminAttemptsToCreateReservedRole_ShouldReturn400WithReservedCodeAndNoPlatformAccess()
    {
        // 1. Arrange: aprovisionar tenant vía endpoint real y obtener JWT real del TenantAdmin desde SQL Server
        var saPayload = await CreateAndLoginSuperAdminAsync();
        string unique = Guid.NewGuid().ToString("N")[..10];
        string adminEmail = $"tadmin_api_{unique}@empresa.test";
        const string password = "TenantAdminApi123!";

        using var superAdminClient = CreateClient(saPayload.Token);
        var createTenantResp = await superAdminClient.PostAsJsonAsync(
            "/api/v1/platform/tenants",
            new CreateTenantRequest($"Tenant API {unique}", $"TAX-API-{unique}", adminEmail, password));

        Assert.Equal(HttpStatusCode.Created, createTenantResp.StatusCode);
        Guid tenantId = await createTenantResp.Content.ReadFromJsonAsync<Guid>();

        using var anonymousClient = CreateClient();
        var loginResp = await anonymousClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(adminEmail, password));
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var loginPayload = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginPayload);
        Assert.Equal(tenantId, loginPayload.TenantId);

        using var tenantAdminClient = CreateClient(loginPayload.Token);

        // 2. Act & Assert: intentar crear rol reservado (exacto y variante) -> 400 Bad Request + código "role.name_reserved"
        foreach (string reservedAttempt in new[] { PlatformRoles.SuperAdmin, $" {PlatformRoles.SuperAdmin.ToLowerInvariant()} " })
        {
            var createRoleResp = await tenantAdminClient.PostAsJsonAsync(
                "/api/v1/roles",
                new CreateRoleRequest(reservedAttempt, "Intento de escalada", [Permissions.Roles.View]));

            Assert.Equal(HttpStatusCode.BadRequest, createRoleResp.StatusCode);
            var json = await createRoleResp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("code", out var codeEl), $"Se esperaba la propiedad 'code' en ProblemDetails: {json}");
            Assert.Equal(RoleErrorCodes.NameReserved, codeEl.GetString());
        }

        // 3. Act & Assert: confirmar que el TenantAdmin tampoco obtiene privilegios de plataforma
        var platformResp = await tenantAdminClient.GetAsync("/api/v1/platform/tenants");
        Assert.Equal(HttpStatusCode.Forbidden, platformResp.StatusCode);
    }

    [Fact]
    public async Task AuthAndRoles_AgainstSqlServer_CrossTenantRoleIsolation_MustPreventTenantAFromResolvingTenantBRole()
    {
        Guid tenantAId = Guid.NewGuid();
        Guid tenantBId = Guid.NewGuid();
        string unique = Guid.NewGuid().ToString("N")[..10];

        Role roleA;
        Role roleB;
        User userA;
        const string passwordA = "TenantAPassword123!";
        string emailA = $"admin_a_{unique}@tenanta.test";

        // 1. Sembrar roles y usuarios en Tenant A y Tenant B
        await using (var dbA = _fixture.CreateDbContext(tenantAId))
        {
            using var scope = _factory.Services.CreateScope();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            roleA = Role.CreateSystemRole(tenantAId, TenantRoles.TenantAdmin, "Admin A", [Permissions.Users.Update, Permissions.Roles.View], TimeProvider.System);
            userA = User.Create(
                new Email(emailA),
                new PasswordHash(hasher.HashPassword(passwordA)),
                roleA.Id,
                tenantAId,
                "Admin",
                "A",
                TimeProvider.System);
            dbA.Roles.Add(roleA);
            dbA.Users.Add(userA);
            await dbA.SaveChangesAsync();
        }

        await using (var dbB = _fixture.CreateDbContext(tenantBId))
        {
            roleB = Role.CreateSystemRole(tenantBId, TenantRoles.Supervisor, "Supervisor B", [Permissions.Sales.Create], TimeProvider.System);
            dbB.Roles.Add(roleB);
            await dbB.SaveChangesAsync();
        }

        // 2. Verificar bajo SQL Server + SqlServerAuthenticationSessionScope (RLS activo) que Tenant A jamás resuelve el rol de Tenant B
        await using (var dbLookup = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            var scopeService = new SqlServerAuthenticationSessionScope(dbLookup);
            await using var session = await scopeService.BeginAsync();
            await session.SetTenantScopeAsync(tenantAId);

            var crossTenantRole = await dbLookup.Roles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == roleB.Id);

            Assert.Null(crossTenantRole);
        }

        // 3. Verificar vía HTTP que el TenantAdmin de Tenant A no puede asignar el RoleId de Tenant B a su usuario
        using var anonymousClient = CreateClient();
        var loginResp = await anonymousClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(emailA, passwordA));
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var loginPayload = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginPayload);

        using var tenantAClient = CreateClient(loginPayload.Token);
        var patchResp = await tenantAClient.PatchAsJsonAsync(
            $"/api/v1/users/{userA.Id}/role",
            new UpdateUserRoleRequest(roleB.Id));

        Assert.Equal(HttpStatusCode.NotFound, patchResp.StatusCode);
    }

    [Fact]
    public async Task ConsecutiveHttpRequests_AgainstSqlServer_FromDistinctTenants_MustNotLeakOrShareTenantContext()
    {
        // 1. Aprovisionar Tenant A y Tenant B vía plataforma real en SQL Server
        var saPayload = await CreateAndLoginSuperAdminAsync();
        string unique = Guid.NewGuid().ToString("N")[..10];
        const string password = "ConsecutiveTenant123!";
        string emailA = $"admin_seq_a_{unique}@tenanta.test";
        string emailB = $"admin_seq_b_{unique}@tenantb.test";

        using var superAdminClient = CreateClient(saPayload.Token);
        var createAResp = await superAdminClient.PostAsJsonAsync(
            "/api/v1/platform/tenants",
            new CreateTenantRequest($"Seq Tenant A {unique}", $"TAX-SA-{unique}", emailA, password));
        Assert.Equal(HttpStatusCode.Created, createAResp.StatusCode);
        Guid tenantAId = await createAResp.Content.ReadFromJsonAsync<Guid>();

        var createBResp = await superAdminClient.PostAsJsonAsync(
            "/api/v1/platform/tenants",
            new CreateTenantRequest($"Seq Tenant B {unique}", $"TAX-SB-{unique}", emailB, password));
        Assert.Equal(HttpStatusCode.Created, createBResp.StatusCode);
        Guid tenantBId = await createBResp.Content.ReadFromJsonAsync<Guid>();
        Assert.NotEqual(tenantAId, tenantBId);

        // 2. Obtener JWT reales de ambos tenants y crear un rol distintivo en cada uno
        using var loginClient = CreateClient();
        var loginA = await (await loginClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(emailA, password)))
            .Content.ReadFromJsonAsync<LoginResponse>();
        var loginB = await (await loginClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(emailB, password)))
            .Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(loginA);
        Assert.NotNull(loginB);
        Assert.Equal(tenantAId, loginA.TenantId);
        Assert.Equal(tenantBId, loginB.TenantId);

        string roleNameA = $"RolExclusivoA_{unique}";
        string roleNameB = $"RolExclusivoB_{unique}";

        using var sharedHttpClient = CreateClient();

        sharedHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginA.Token);
        var createRoleAResp = await sharedHttpClient.PostAsJsonAsync(
            "/api/v1/roles",
            new CreateRoleRequest(roleNameA, "Rol exclusivo de Tenant A", [Permissions.Roles.View]));
        Assert.Equal(HttpStatusCode.Created, createRoleAResp.StatusCode);

        sharedHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginB.Token);
        var createRoleBResp = await sharedHttpClient.PostAsJsonAsync(
            "/api/v1/roles",
            new CreateRoleRequest(roleNameB, "Rol exclusivo de Tenant B", [Permissions.Roles.View]));
        Assert.Equal(HttpStatusCode.Created, createRoleBResp.StatusCode);

        // 3. Peticiones HTTP consecutivas alternando Tenant A -> Tenant B -> Tenant A
        //    Cada request debe resolver y aislar su propio TenantId bajo HybridCache + RLS.
        sharedHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginA.Token);
        var rolesAResp1 = await sharedHttpClient.GetAsync("/api/v1/roles");
        Assert.Equal(HttpStatusCode.OK, rolesAResp1.StatusCode);
        var rolesA1 = await rolesAResp1.Content.ReadFromJsonAsync<List<RoleDto>>();
        Assert.NotNull(rolesA1);
        Assert.Contains(rolesA1, r => r.Name == roleNameA);
        Assert.DoesNotContain(rolesA1, r => r.Name == roleNameB);

        sharedHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginB.Token);
        var rolesBResp = await sharedHttpClient.GetAsync("/api/v1/roles");
        Assert.Equal(HttpStatusCode.OK, rolesBResp.StatusCode);
        var rolesB = await rolesBResp.Content.ReadFromJsonAsync<List<RoleDto>>();
        Assert.NotNull(rolesB);
        Assert.Contains(rolesB, r => r.Name == roleNameB);
        Assert.DoesNotContain(rolesB, r => r.Name == roleNameA);

        sharedHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginA.Token);
        var rolesAResp2 = await sharedHttpClient.GetAsync("/api/v1/roles");
        Assert.Equal(HttpStatusCode.OK, rolesAResp2.StatusCode);
        var rolesA2 = await rolesAResp2.Content.ReadFromJsonAsync<List<RoleDto>>();
        Assert.NotNull(rolesA2);
        Assert.Contains(rolesA2, r => r.Name == roleNameA);
        Assert.DoesNotContain(rolesA2, r => r.Name == roleNameB);

        // Petición anónima consecutiva en el mismo cliente no hereda el scope de Tenant A
        sharedHttpClient.DefaultRequestHeaders.Authorization = null;
        var anonRolesResp = await sharedHttpClient.GetAsync("/api/v1/roles");
        Assert.Equal(HttpStatusCode.Unauthorized, anonRolesResp.StatusCode);
    }

    private static void AssertJwtRoleClaim(string token, string expectedRole)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role);
        Assert.NotNull(roleClaim);
        Assert.Equal(expectedRole, roleClaim.Value);
    }

    private static async Task<(int? AllowGlobalUserLookup, Guid? TenantId, int? IsSuperAdmin)> ReadSessionContextAsync(PosDbContext dbContext)
    {
        var connection = dbContext.Database.GetDbConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                TRY_CAST(SESSION_CONTEXT(N'AllowGlobalUserLookup') AS INT),
                TRY_CAST(SESSION_CONTEXT(N'TenantId') AS UNIQUEIDENTIFIER),
                TRY_CAST(SESSION_CONTEXT(N'IsSuperAdmin') AS INT);
            """;

        using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        int? allowGlobal = await reader.IsDBNullAsync(0) ? null : reader.GetInt32(0);
        Guid? tenantId = await reader.IsDBNullAsync(1) ? null : reader.GetGuid(1);
        int? isSuperAdmin = await reader.IsDBNullAsync(2) ? null : reader.GetInt32(2);
        return (allowGlobal, tenantId, isSuperAdmin);
    }
}
