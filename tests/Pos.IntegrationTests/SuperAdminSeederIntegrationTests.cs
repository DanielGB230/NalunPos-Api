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
            Assert.Contains(Permissions.Platform.DeadLetterView, superAdminRole.Permissions);
            Assert.Contains(Permissions.Platform.DeadLetterReprocess, superAdminRole.Permissions);

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
            Assert.Equal(1, superAdminRole.Permissions.Count(p => string.Equals(p, Permissions.Platform.DeadLetterView, StringComparison.OrdinalIgnoreCase)));
            Assert.Equal(1, superAdminRole.Permissions.Count(p => string.Equals(p, Permissions.Platform.DeadLetterReprocess, StringComparison.OrdinalIgnoreCase)));

            var usersCount = await dbContext.Users
                .IgnoreQueryFilters()
                .CountAsync(u => u.RoleId == Role.SuperAdminRoleId);

            Assert.True(usersCount >= 1, "Debe existir al menos un usuario con el rol SuperAdminRoleId.");
        }
    }
}
