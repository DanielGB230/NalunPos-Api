using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Common.Interfaces;
using Pos.Application.Platform.Tenants.Commands.CreateTenant;
using Pos.Application.Platform.Tenants.Queries.GetTenants;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
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

        var tokenGenerator = new JwtTokenGenerator(configuration);

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
        var userInDb = await authLookup.FindByEmailAsync(adminEmail);
        Assert.NotNull(userInDb);
        Assert.NotEqual(Guid.Empty, userInDb.RoleId);
        Assert.Equal(createdTenantId, userInDb.TenantId);
        Assert.NotNull(userInDb.PasswordHash.Value);

        // 4. JWT Generation & Payload Verification
        string token = tokenGenerator.GenerateToken(userInDb);
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
}
