using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Domain.Enums;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Seed;
using Xunit;

namespace Pos.Infrastructure.Tests;

public class SuperAdminSeederTests
{
    private readonly PasswordHasher _passwordHasher = new();

    [Fact]
    public async Task SeedAsync_OnEmptyDatabase_ShouldSeedSuperAdmin()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["SuperAdminSettings:Email"] = "superadmin@nalunpos.com",
            ["SuperAdminSettings:Password"] = "SuperPass123!",
            ["SuperAdminSettings:FirstName"] = "Super",
            ["SuperAdminSettings:LastName"] = "Admin"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        await using var context = new PosDbContext(options);
        var seeder = new SuperAdminSeeder(context, _passwordHasher, config, NullLogger<SuperAdminSeeder>.Instance);

        // Act
        await seeder.SeedAsync();

        // Assert
        var seededUser = await context.Users.FirstOrDefaultAsync(u => u.Role == UserRole.SuperAdmin);
        Assert.NotNull(seededUser);
        Assert.Equal("superadmin@nalunpos.com", seededUser.Email.Value);
        Assert.Null(seededUser.TenantId);
        Assert.True(_passwordHasher.Verify("SuperPass123!", seededUser.PasswordHash));
    }

    [Fact]
    public async Task SeedAsync_WhenSuperAdminAlreadyExists_ShouldBeIdempotentAndNotDuplicate()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["SuperAdminSettings:Email"] = "superadmin@nalunpos.com",
            ["SuperAdminSettings:Password"] = "SuperPass123!"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        await using var context = new PosDbContext(options);
        var seeder = new SuperAdminSeeder(context, _passwordHasher, config, NullLogger<SuperAdminSeeder>.Instance);

        // Act — primera ejecución
        await seeder.SeedAsync();

        // Act — segunda ejecución (idempotencia)
        await seeder.SeedAsync();

        // Assert — solo debe existir 1 SuperAdmin
        int count = await context.Users.CountAsync(u => u.Role == UserRole.SuperAdmin);
        Assert.Equal(1, count);
    }
}
