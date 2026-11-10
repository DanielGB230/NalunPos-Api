using Microsoft.EntityFrameworkCore;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence.Context;
using Xunit;

namespace Pos.Infrastructure.Tests;

public class AuthUserLookupTests
{
    private sealed class RecordingSessionScope : IAuthenticationSessionScope
    {
        public List<string> Events { get; } = [];

        public Task<IAuthenticationSession> BeginAsync(CancellationToken cancellationToken = default)
        {
            Events.Add("Begin");
            return Task.FromResult<IAuthenticationSession>(new RecordingSession(Events));
        }

        private sealed class RecordingSession(List<string> events) : IAuthenticationSession
        {
            public Task EnableGlobalUserLookupAsync(CancellationToken cancellationToken = default)
            {
                events.Add("EnableGlobalLookup");
                return Task.CompletedTask;
            }

            public Task DisableGlobalUserLookupAsync(CancellationToken cancellationToken = default)
            {
                events.Add("DisableGlobalLookup");
                return Task.CompletedTask;
            }

            public Task SetTenantScopeAsync(Guid tenantId, CancellationToken cancellationToken = default)
            {
                events.Add($"SetTenantScope:{tenantId}");
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                events.Add("Dispose");
                return ValueTask.CompletedTask;
            }
        }
    }

    private static PosDbContext CreateInMemoryContext(string dbName, Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new PosDbContext(options, currentTenantId: tenantId);
    }

    [Fact]
    public async Task FindByEmailAsync_WithValidSuperAdmin_ShouldReturnSuperAdminAndDisposeScopeWithoutSettingTenant()
    {
        string dbName = $"AuthLookup_{Guid.NewGuid():N}";
        await using (var seedDb = CreateInMemoryContext(dbName, PlatformScope.TenantId))
        {
            var user = User.Create(
                new Email("sa@platform.com"),
                new PasswordHash("HASH"),
                Role.SuperAdminRoleId,
                tenantId: null,
                "Super",
                "Admin",
                TimeProvider.System);
            seedDb.Users.Add(user);
            await seedDb.SaveChangesAsync();
        }

        await using var db = CreateInMemoryContext(dbName, PlatformScope.TenantId);
        var scope = new RecordingSessionScope();
        var lookup = new AuthUserLookup(db, scope);

        var result = await lookup.FindByEmailAsync("sa@platform.com");

        Assert.NotNull(result);
        Assert.Equal(PlatformRoles.SuperAdmin, result.Role);
        Assert.Equal(["Begin", "EnableGlobalLookup", "DisableGlobalLookup", "Dispose"], scope.Events);
    }

    [Fact]
    public async Task FindByEmailAsync_WithValidTenantUser_ShouldSetTenantScopeAndDisposeBalanceably()
    {
        string dbName = $"AuthLookup_{Guid.NewGuid():N}";
        Guid tenantId = Guid.NewGuid();
        Guid roleId;

        await using (var seedDb = CreateInMemoryContext(dbName, tenantId))
        {
            var role = Role.CreateSystemRole(tenantId, TenantRoles.Supervisor, "Supervisor", null, TimeProvider.System);
            roleId = role.Id;
            var user = User.Create(
                new Email("sup@tenant.com"),
                new PasswordHash("HASH"),
                role.Id,
                tenantId,
                "Ana",
                "Supervisor",
                TimeProvider.System);
            seedDb.Roles.Add(role);
            seedDb.Users.Add(user);
            await seedDb.SaveChangesAsync();
        }

        await using var db = CreateInMemoryContext(dbName);
        var scope = new RecordingSessionScope();
        var lookup = new AuthUserLookup(db, scope);

        var result = await lookup.FindByEmailAsync("sup@tenant.com");

        Assert.NotNull(result);
        Assert.Equal(TenantRoles.Supervisor, result.Role);
        Assert.Equal(roleId, result.User.RoleId);
        Assert.Equal(
            ["Begin", "EnableGlobalLookup", "DisableGlobalLookup", $"SetTenantScope:{tenantId}", "Dispose"],
            scope.Events);
    }

    [Fact]
    public async Task FindByEmailAsync_WhenTenantUserHasRoleNamedSuperAdmin_ShouldFailClosedAndReturnNull()
    {
        string dbName = $"AuthLookup_{Guid.NewGuid():N}";
        Guid tenantId = Guid.NewGuid();

        await using (var seedDb = CreateInMemoryContext(dbName, tenantId))
        {
            var role = Role.Create(tenantId, "CustomTemp", "Temp", TimeProvider.System);
            // Simula corrupción directa en BD saltando el dominio
            typeof(Role).GetProperty(nameof(Role.Name))!.SetValue(role, PlatformRoles.SuperAdmin);

            var user = User.Create(
                new Email("escalated@tenant.com"),
                new PasswordHash("HASH"),
                role.Id,
                tenantId,
                "Eve",
                "Attacker",
                TimeProvider.System);

            seedDb.Roles.Add(role);
            seedDb.Users.Add(user);
            await seedDb.SaveChangesAsync();
        }

        await using var db = CreateInMemoryContext(dbName);
        var scope = new RecordingSessionScope();
        var lookup = new AuthUserLookup(db, scope);

        var result = await lookup.FindByEmailAsync("escalated@tenant.com");

        Assert.Null(result);
        Assert.Equal("Dispose", scope.Events.Last());
    }

    [Fact]
    public async Task FindByEmailAsync_WhenTenantUserHasSuperAdminRoleId_ShouldFailClosedAndReturnNull()
    {
        string dbName = $"AuthLookup_{Guid.NewGuid():N}";
        Guid tenantId = Guid.NewGuid();

        await using (var seedDb = CreateInMemoryContext(dbName, tenantId))
        {
            var user = User.Create(
                new Email("hybrid@tenant.com"),
                new PasswordHash("HASH"),
                Role.SuperAdminRoleId,
                tenantId,
                "Hybrid",
                "User",
                TimeProvider.System);
            seedDb.Users.Add(user);
            await seedDb.SaveChangesAsync();
        }

        await using var db = CreateInMemoryContext(dbName);
        var scope = new RecordingSessionScope();
        var lookup = new AuthUserLookup(db, scope);

        var result = await lookup.FindByEmailAsync("hybrid@tenant.com");

        Assert.Null(result);
        Assert.Equal(["Begin", "EnableGlobalLookup", "DisableGlobalLookup", "Dispose"], scope.Events);
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("  superadmin  ")]
    [InlineData("SUPERADMIN")]
    public async Task FindByEmailAsync_WhenTenantUserHasRoleNamedSuperAdminVariant_ShouldFailClosedAndReturnNull(string spoofedRoleName)
    {
        string dbName = $"AuthLookup_{Guid.NewGuid():N}";
        Guid tenantId = Guid.NewGuid();

        await using (var seedDb = CreateInMemoryContext(dbName, tenantId))
        {
            var role = Role.Create(tenantId, "CustomTemp", "Temp", TimeProvider.System);
            typeof(Role).GetProperty(nameof(Role.Name))!.SetValue(role, spoofedRoleName);

            var user = User.Create(
                new Email("spoofed@tenant.com"),
                new PasswordHash("HASH"),
                role.Id,
                tenantId,
                "Eve",
                "Attacker",
                TimeProvider.System);

            seedDb.Roles.Add(role);
            seedDb.Users.Add(user);
            await seedDb.SaveChangesAsync();
        }

        await using var db = CreateInMemoryContext(dbName);
        var scope = new RecordingSessionScope();
        var lookup = new AuthUserLookup(db, scope);

        var result = await lookup.FindByEmailAsync("spoofed@tenant.com");

        Assert.Null(result);
        Assert.Equal("Dispose", scope.Events.Last());
    }

    [Fact]
    public async Task FindByEmailAsync_WhenNullTenantUserLacksSuperAdminRoleId_ShouldFailClosedAndReturnNull()
    {
        string dbName = $"AuthLookup_{Guid.NewGuid():N}";
        await using (var seedDb = CreateInMemoryContext(dbName, PlatformScope.TenantId))
        {
            var user = User.Create(
                new Email("orphan@platform.com"),
                new PasswordHash("HASH"),
                Guid.NewGuid(),
                tenantId: null,
                "Orphan",
                "User",
                TimeProvider.System);
            seedDb.Users.Add(user);
            await seedDb.SaveChangesAsync();
        }

        await using var db = CreateInMemoryContext(dbName);
        var scope = new RecordingSessionScope();
        var lookup = new AuthUserLookup(db, scope);

        var result = await lookup.FindByEmailAsync("orphan@platform.com");

        Assert.Null(result);
        Assert.Equal(["Begin", "EnableGlobalLookup", "DisableGlobalLookup", "Dispose"], scope.Events);
    }

    [Fact]
    public async Task FindByEmailAsync_WhenUserPointsToRoleFromAnotherTenant_ShouldFailClosedAndReturnNull()
    {
        string dbName = $"AuthLookup_{Guid.NewGuid():N}";
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();

        await using (var seedDb = CreateInMemoryContext(dbName, tenantB))
        {
            var roleB = Role.CreateSystemRole(tenantB, TenantRoles.TenantAdmin, "Admin B", null, TimeProvider.System);
            var userA = User.Create(
                new Email("usera@tenanta.com"),
                new PasswordHash("HASH"),
                roleB.Id,
                tenantA,
                "User",
                "A",
                TimeProvider.System);
            seedDb.Roles.Add(roleB);
            seedDb.Users.Add(userA);
            await seedDb.SaveChangesAsync();
        }

        await using var db = CreateInMemoryContext(dbName);
        var scope = new RecordingSessionScope();
        var lookup = new AuthUserLookup(db, scope);

        var result = await lookup.FindByEmailAsync("usera@tenanta.com");

        Assert.Null(result);
        Assert.Equal("Dispose", scope.Events.Last());
    }
}
