using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pos.Api.Options;
using Pos.Application.Common.Authorization;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence.Seed;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

[Collection("IntegrationTests")]
public class SuperAdminSeederIntegrationTests
{
    private readonly MsSqlTestFixture _fixture;

    public SuperAdminSeederIntegrationTests(MsSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SuperAdminSeeder_OnCleanDatabase_SeedsRoleWithDeadLetterPermissions_AndIsIdempotentWhenRunTwice()
    {
        // Arrange
        var settings = Options.Create(new SuperAdminSettings
        {
            Email = $"seeder_{Guid.NewGuid():N}@enterprise.com",
            Password = "SuperAdminPassword123!",
            FirstName = "Super",
            LastName = "Admin"
        });

        var passwordHasher = new PasswordHasher();
        var timeProvider = TimeProvider.System;

        // Act 1: Primera ejecución de siembra
        using (var dbContext = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            var seeder = new SuperAdminSeeder(
                dbContext,
                passwordHasher,
                settings,
                NullLogger<SuperAdminSeeder>.Instance,
                timeProvider);

            await seeder.SeedAsync();
        }

        // Assert 1: Verificar permisos y rol sembrados
        using (var dbContext = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            var superAdminRole = await dbContext.Set<Role>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == Role.SuperAdminRoleId);

            Assert.NotNull(superAdminRole);
            foreach (var perm in SuperAdminRoleCatalog.Permissions)
            {
                Assert.Contains(perm, superAdminRole.Permissions);
            }

            var superAdminUser = await dbContext.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.RoleId == Role.SuperAdminRoleId);

            Assert.NotNull(superAdminUser);
        }

        // Act 2: Segunda ejecución de siembra (idempotencia)
        using (var dbContext = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            var seeder = new SuperAdminSeeder(
                dbContext,
                passwordHasher,
                settings,
                NullLogger<SuperAdminSeeder>.Instance,
                timeProvider);

            await seeder.SeedAsync();
        }

        // Assert 2: Verificar idempotencia estricta sin duplicados ni errores
        using (var dbContext = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            var superAdminRole = await dbContext.Set<Role>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == Role.SuperAdminRoleId);

            Assert.NotNull(superAdminRole);
            foreach (var perm in SuperAdminRoleCatalog.Permissions)
            {
                Assert.Equal(1, superAdminRole.Permissions.Count(p => string.Equals(p, perm, StringComparison.OrdinalIgnoreCase)));
            }
            Assert.Equal(SuperAdminRoleCatalog.Permissions.Count, superAdminRole.Permissions.Count);

            var usersCount = await dbContext.Users
                .IgnoreQueryFilters()
                .CountAsync(u => u.RoleId == Role.SuperAdminRoleId);

            Assert.True(usersCount >= 1, "Debe existir al menos un usuario con el rol SuperAdminRoleId.");
        }
    }

    [Fact]
    public async Task SuperAdminSeeder_WhenRolePreExistsWithDeadLetterOnly_MigratesPermissionsToExactCatalog_AndIsIdempotent()
    {
        // Arrange: Crear rol preexistente con solo DeadLetter
        using (var dbContext = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            var existingRole = await dbContext.Set<Role>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == Role.SuperAdminRoleId);

            if (existingRole == null)
            {
                existingRole = Role.CreatePlatformRole(
                    Role.SuperAdminRoleId,
                    PlatformScope.TenantId,
                    PlatformRoles.SuperAdmin,
                    "Administrador Global",
                    new[] { Permissions.Platform.DeadLetterView, Permissions.Platform.DeadLetterReprocess },
                    TimeProvider.System);
                dbContext.Set<Role>().Add(existingRole);
            }
            else
            {
                existingRole.UpdatePermissions(new[] { Permissions.Platform.DeadLetterView, Permissions.Platform.DeadLetterReprocess });
            }

            await dbContext.SaveChangesAsync();
        }

        var settings = Options.Create(new SuperAdminSettings
        {
            Email = $"seeder_mig_{Guid.NewGuid():N}@enterprise.com",
            Password = "SuperAdminPassword123!",
            FirstName = "Super",
            LastName = "Admin"
        });

        var passwordHasher = new PasswordHasher();
        var timeProvider = TimeProvider.System;

        // Act 1: Ejecutar seeder sobre el rol preexistente
        using (var dbContext = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            var seeder = new SuperAdminSeeder(
                dbContext,
                passwordHasher,
                settings,
                NullLogger<SuperAdminSeeder>.Instance,
                timeProvider);

            await seeder.SeedAsync();
        }

        // Assert 1: El rol debe haber migrado a contener exactamente los permisos del catálogo
        using (var dbContext = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            var superAdminRole = await dbContext.Set<Role>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == Role.SuperAdminRoleId);

            Assert.NotNull(superAdminRole);
            Assert.Equal(SuperAdminRoleCatalog.Permissions.Count, superAdminRole.Permissions.Count);
            foreach (var perm in SuperAdminRoleCatalog.Permissions)
            {
                Assert.Contains(perm, superAdminRole.Permissions);
            }
        }

        // Act 2: Segunda ejecución para verificar idempotencia post-migración
        using (var dbContext = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            var seeder = new SuperAdminSeeder(
                dbContext,
                passwordHasher,
                settings,
                NullLogger<SuperAdminSeeder>.Instance,
                timeProvider);

            await seeder.SeedAsync();
        }

        // Assert 2: Permisos idénticos, sin duplicados
        using (var dbContext = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            var superAdminRole = await dbContext.Set<Role>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == Role.SuperAdminRoleId);

            Assert.NotNull(superAdminRole);
            Assert.Equal(SuperAdminRoleCatalog.Permissions.Count, superAdminRole.Permissions.Count);
            foreach (var perm in SuperAdminRoleCatalog.Permissions)
            {
                Assert.Equal(1, superAdminRole.Permissions.Count(p => string.Equals(p, perm, StringComparison.OrdinalIgnoreCase)));
            }
        }
    }
}
