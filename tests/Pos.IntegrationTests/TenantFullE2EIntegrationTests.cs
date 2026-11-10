using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Platform.Tenants.Commands.CreateTenant;
using Pos.Application.Platform.Tenants.Queries.GetTenants;
using Pos.Domain.Common;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence.Context;
using Pos.IntegrationTests.Fixtures;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Xunit;

namespace Pos.IntegrationTests;

[Collection("IntegrationTests")]
public class TenantFullE2EIntegrationTests
{
    private readonly MsSqlTestFixture _fixture;

    public TenantFullE2EIntegrationTests(MsSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FullE2E_SuperAdminCreatesTenant_TenantAndAdminUserCreatedInDb_JWTContainsValidTenantId()
    {
        // 1. Arrange: Setup Service Provider
        var serviceProvider = _fixture.CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var dbContext = sp.GetRequiredService<PosDbContext>();
        
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationManager();
        configuration["JwtSettings:Secret"] = "SuperSecretEnterpriseJwtKey_LongEnoughFor256Bits_NalunPos2026!";
        configuration["JwtSettings:Issuer"] = "NalunPosApi";
        configuration["JwtSettings:Audience"] = "NalunPosClients";

        var tokenGenerator = new JwtTokenGenerator(configuration, TimeProvider.System);

        string timestamp = DateTime.UtcNow.ToString("HHmmssff", System.Globalization.CultureInfo.InvariantCulture);
        string companyName = $"Empresa E2E Real {timestamp} S.A.C.";
        string taxId = $"2070{timestamp}";
        string adminEmail = $"tenantadmin_{timestamp}@demopos.com";
        string adminPassword = "Password123!";

        // 2. Act: SuperAdmin Creates Tenant via CreateTenantCommand
        var createCommand = new CreateTenantCommand(companyName, taxId, adminEmail, adminPassword);
        var createResult = await dispatcher.SendAsync(createCommand);

        // Assert Step 1: Creation Success
        Assert.True(createResult.IsSuccess);
        Guid createdTenantId = createResult.Value;
        Assert.NotEqual(Guid.Empty, createdTenantId);

        // 3. Database Verification (Query SQL Context directly)
        var tenantInDb = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == createdTenantId);
        Assert.NotNull(tenantInDb);
        Assert.Equal(companyName, tenantInDb.Name);
        Assert.Equal(taxId, tenantInDb.TaxId.Value);

        var authLookup = sp.GetRequiredService<IAuthUserLookup>();
        var authUser = await authLookup.FindByEmailAsync(adminEmail);
        Assert.NotNull(authUser);
        var userInDb = authUser.User;
        Assert.NotEqual(Guid.Empty, userInDb.RoleId);
        Assert.Equal(createdTenantId, userInDb.TenantId);
        Assert.NotNull(userInDb.PasswordHash.Value);

        // 4. JWT Generation & Payload Verification
        string token = tokenGenerator.GenerateToken(userInDb, authUser.Role);
        Assert.False(string.IsNullOrWhiteSpace(token));

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var tenantIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "tenant_id" || c.Type == "tenantId")?.Value;
        var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "roleId")?.Value;
        var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == "sub")?.Value;

        Assert.NotNull(tenantIdClaim);
        Assert.Equal(createdTenantId.ToString(), tenantIdClaim);
        Assert.Equal(userInDb.RoleId.ToString(), roleClaim);

        // 5. Query Verification via GetTenantsQuery
        var getTenantsResult = await dispatcher.SendAsync(new GetTenantsQuery(1, 10, taxId));
        Assert.Single(getTenantsResult.Items);
        Assert.Equal(companyName, getTenantsResult.Items[0].Name);
    }

    [Fact]
    public async Task FullE2E_CreateTenant_ProvisionsAllFourSystemRolesAndPermissionsInSqlServer_WithoutSeeder()
    {
        // 1. Arrange: Contexto SuperAdmin ejecutando el caso de uso real CreateTenantCommand
        var superAdminSp = _fixture.CreateServiceProvider();
        using var superAdminScope = superAdminSp.CreateScope();
        var dispatcher = superAdminScope.ServiceProvider.GetRequiredService<IDispatcher>();

        string unique = Guid.NewGuid().ToString("N")[..12];
        string taxId = $"RUC-{unique}";
        string adminEmail = $"prov_admin_{unique}@demopos.com";

        var createCommand = new CreateTenantCommand(
            $"Aprovisionada {unique} S.A.C.",
            taxId,
            adminEmail,
            "Password123!");

        // 2. Act: Crear el tenant en SQL Server sin invocar DefaultRoleSeeder
        var createResult = await dispatcher.SendAsync(createCommand);
        Assert.True(createResult.IsSuccess);
        Guid createdTenantId = createResult.Value;

        // 3. Assert BD SQL Server bajo contexto RLS del nuevo tenant (sin seeder)
        await using var tenantDb = _fixture.CreateDbContext(createdTenantId);
        var rolesInDb = await tenantDb.Roles
            .AsNoTracking()
            .ToListAsync();

        Assert.Equal(DefaultTenantRoleCatalog.All.Count, rolesInDb.Count);

        foreach (var expectedDef in DefaultTenantRoleCatalog.All)
        {
            var role = Assert.Single(rolesInDb, r => r.Name == expectedDef.Name);
            Assert.Equal(createdTenantId, role.TenantId);
            Assert.Equal(expectedDef.Description, role.Description);
            Assert.True(
                expectedDef.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(role.Permissions),
                $"Los permisos persistidos para '{expectedDef.Name}' en SQL Server no coinciden con DefaultTenantRoleCatalog.");
        }

        // Verificar que el usuario administrador apunta al rol TenantAdmin recién aprovisionado
        var tenantAdminRole = Assert.Single(rolesInDb, r => r.Name == TenantRoles.TenantAdmin);
        var adminEmailVo = new Pos.Domain.ValueObjects.Email(adminEmail);
        var adminUser = await tenantDb.Users.AsNoTracking().SingleAsync(u => u.Email == adminEmailVo);
        Assert.Equal(tenantAdminRole.Id, adminUser.RoleId);

        // Verificar resolución real de permisos vía ICurrentUserPermissions contra SQL Server (prohibido usar seeder)
        var tenantSp = _fixture.CreateServiceProvider(createdTenantId, seedRolesIfMissing: false);
        using var tenantScope = tenantSp.CreateScope();
        var permissionsService = tenantScope.ServiceProvider.GetRequiredService<ICurrentUserPermissions>();

        var resolvedPermissions = await permissionsService.GetPermissionsAsync(createdTenantId, adminUser.RoleId);
        Assert.Equal(tenantAdminRole.Permissions.Count, resolvedPermissions.Count);
        Assert.Contains(Permissions.Users.Create, resolvedPermissions);
        Assert.Contains(Permissions.Products.Create, resolvedPermissions);
        Assert.Contains(Permissions.Sales.Create, resolvedPermissions);
    }
}
