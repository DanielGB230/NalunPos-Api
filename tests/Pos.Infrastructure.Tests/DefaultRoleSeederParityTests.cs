using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Platform.Tenants.Commands.CreateTenant;
using Pos.Domain.Common;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.Persistence.Seed;
using Xunit;

namespace Pos.Infrastructure.Tests;

public class DefaultRoleSeederParityTests
{
    private sealed class StubAuthUserLookup : IAuthUserLookup
    {
        public Task<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult<AuthUser?>(null);

        public Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    [Fact]
    public async Task CreateTenantCommandHandler_And_DefaultRoleSeeder_ShouldProduceIdenticalRolesAndPermissions()
    {
        // Arrange: Base InMemory compartida pero dos tenants aislados
        var dbName = $"RoleParity_{Guid.NewGuid():N}";
        var tenantForCreateTenantOptions = new DbContextOptionsBuilder<PosDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        Guid provisionedTenantId;
        await using (var createContext = new PosDbContext(tenantForCreateTenantOptions, currentTenantId: PlatformScope.TenantId))
        {
            var handler = new CreateTenantCommandHandler(
                new TenantRepository(createContext),
                new UserRepository(createContext),
                new StubAuthUserLookup(),
                new BranchRepository(createContext),
                new WarehouseRepository(createContext),
                new RoleRepository(createContext),
                new PasswordHasher(),
                createContext,
                TimeProvider.System);

            var createResult = await handler.HandleAsync(
                new CreateTenantCommand("Tenant A", "20601112223", "admin@tenanta.com", "Password123!"),
                CancellationToken.None);

            Assert.True(createResult.IsSuccess);
            provisionedTenantId = createResult.Value;
        }

        Guid seededTenantId = Guid.NewGuid();
        await using (var seedContext = new PosDbContext(tenantForCreateTenantOptions, currentTenantId: seededTenantId))
        {
            var seeder = new DefaultRoleSeeder(seedContext, NullLogger<DefaultRoleSeeder>.Instance, TimeProvider.System);
            await seeder.SeedRolesForTenantAsync(seededTenantId, CancellationToken.None);
        }

        // Act & Assert: Comparar ambos conjuntos de roles desde BD
        await using var verifyProvisionedDb = new PosDbContext(tenantForCreateTenantOptions, currentTenantId: provisionedTenantId);
        await using var verifySeededDb = new PosDbContext(tenantForCreateTenantOptions, currentTenantId: seededTenantId);

        var provisionedRoles = await verifyProvisionedDb.Roles.AsNoTracking().ToListAsync();
        var seededRoles = await verifySeededDb.Roles.AsNoTracking().ToListAsync();

        Assert.Equal(DefaultTenantRoleCatalog.All.Count, provisionedRoles.Count);
        Assert.Equal(DefaultTenantRoleCatalog.All.Count, seededRoles.Count);

        foreach (var expected in DefaultTenantRoleCatalog.All)
        {
            var provisioned = Assert.Single(provisionedRoles, r => r.Name == expected.Name);
            var seeded = Assert.Single(seededRoles, r => r.Name == expected.Name);

            Assert.Equal(seeded.Description, provisioned.Description);
            Assert.Equal(expected.Description, provisioned.Description);

            var provisionedPerms = provisioned.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var seededPerms = seeded.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.True(
                provisionedPerms.SetEquals(seededPerms),
                $"Los permisos del rol '{expected.Name}' difieren entre CreateTenantCommandHandler y DefaultRoleSeeder.");
            Assert.True(
                provisionedPerms.SetEquals(expected.Permissions),
                $"Los permisos del rol '{expected.Name}' difieren respecto a DefaultTenantRoleCatalog.");
        }
    }

    [Fact]
    public async Task DefaultRoleSeeder_WhenCalledAgainWithMissingPermissions_ShouldReconcileIdempotently()
    {
        // Arrange
        var dbName = $"RoleSeederIdempotent_{Guid.NewGuid():N}";
        Guid tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using (var db = new PosDbContext(options, currentTenantId: tenantId))
        {
            var seeder = new DefaultRoleSeeder(db, NullLogger<DefaultRoleSeeder>.Instance, TimeProvider.System);
            await seeder.SeedRolesForTenantAsync(tenantId, CancellationToken.None);

            var cajeroRole = await db.Roles.FirstAsync(r => r.TenantId == tenantId && r.Name == TenantRoles.Cajero);
            cajeroRole.RemovePermission(Permissions.Sales.Create);
            await db.SaveChangesAsync();
        }

        // Act: Re-ejecutar el seeder sobre el mismo tenant
        await using (var db = new PosDbContext(options, currentTenantId: tenantId))
        {
            var seeder = new DefaultRoleSeeder(db, NullLogger<DefaultRoleSeeder>.Instance, TimeProvider.System);
            await seeder.SeedRolesForTenantAsync(tenantId, CancellationToken.None);
        }

        // Assert
        await using (var verifyDb = new PosDbContext(options, currentTenantId: tenantId))
        {
            var roles = await verifyDb.Roles.AsNoTracking().ToListAsync();
            Assert.Equal(4, roles.Count);

            var cajeroRole = Assert.Single(roles, r => r.Name == TenantRoles.Cajero);
            Assert.Contains(Permissions.Sales.Create, cajeroRole.Permissions);
            var expectedCajero = DefaultTenantRoleCatalog.All.Single(d => d.Name == TenantRoles.Cajero);
            Assert.Equal(expectedCajero.Permissions.Count, cajeroRole.Permissions.Count);
        }
    }
}

