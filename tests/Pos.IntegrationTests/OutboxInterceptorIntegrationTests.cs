using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.IntegrationEvents.Contracts.V1;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Persistence.Context;
using Pos.IntegrationTests.Fixtures;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Pos.IntegrationTests;

[Collection("IntegrationTests")]
public class OutboxInterceptorIntegrationTests
{
    private readonly MsSqlTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public OutboxInterceptorIntegrationTests(MsSqlTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task ChangeRole_WithSuperAdminContext_ShouldGenerateOutboxMessageWithUserTenantId()
    {
        Guid tenantId = Guid.NewGuid();
        User user;
        Role role;
        Role newRole;

        // 1. Arrange: Insert a user and role using a tenant context
        using (var dbSetup = _fixture.CreateDbContext(tenantId))
        {
            role = Role.Create(tenantId, "InitialRole", "Desc");
            newRole = Role.Create(tenantId, "NewRole", "Desc");
            dbSetup.Roles.Add(role);
            dbSetup.Roles.Add(newRole);
            user = User.Create(new Email("test@tenant.com"), new PasswordHash("hash"), role.Id, tenantId, "Test", "User");
            dbSetup.Users.Add(user);
            await dbSetup.SaveChangesAsync();
        }

        // 2. Act: SuperAdmin (tenantId = null) modifies the user's role
        using (var dbTenant = _fixture.CreateDbContext(tenantId))
        {
            user = await dbTenant.Users.FirstAsync(u => u.Id == user.Id);
        }

        using (var dbSuperAdmin = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            dbSuperAdmin.Users.Attach(user);
            user.ChangeRole(newRole.Id, tenantId);
            await dbSuperAdmin.SaveChangesAsync();
        }

        // 3. Assert: Verify the outbox message is generated with the user's tenantId
        using (var dbVerify = _fixture.CreateDbContext(tenantId))
        {
            var messages = await dbVerify.OutboxMessages
                .Where(o => o.Type.Contains(nameof(UserRoleChangedIntegrationEventV1)))
                .ToListAsync();

            var outboxMessage = Assert.Single(messages);
            Assert.Equal(tenantId, outboxMessage.TenantId);

            var integrationEvent = JsonSerializer.Deserialize<UserRoleChangedIntegrationEventV1>(outboxMessage.Content, _jsonOptions);
            Assert.NotNull(integrationEvent);
            Assert.Equal(user.Id, integrationEvent.UserId);
            Assert.Equal(role.Id, integrationEvent.OldRoleId);
            Assert.Equal(newRole.Id, integrationEvent.NewRoleId);
            Assert.Equal(tenantId, integrationEvent.OldTenantId);
            Assert.Equal(tenantId, integrationEvent.NewTenantId);
        }
    }

    [Fact]
    public async Task ChangeRole_WithTenantContext_ShouldGenerateOutboxMessageWithThatTenantId()
    {
        Guid tenantId = Guid.NewGuid();
        User user;
        Role initialRole;
        Role newRole;

        // 1. Arrange: Insert a user and role using a tenant context
        using (var dbSetup = _fixture.CreateDbContext(tenantId))
        {
            initialRole = Role.Create(tenantId, "InitialRole", "Desc");
            newRole = Role.Create(tenantId, "NewRole", "Desc");
            dbSetup.Roles.AddRange(initialRole, newRole);
            user = User.Create(new Email("tenant@tenant.com"), new PasswordHash("hash"), initialRole.Id, tenantId, "Test", "User");
            dbSetup.Users.Add(user);
            await dbSetup.SaveChangesAsync();
        }

        // 2. Act: Same tenant modifies the user's role using DI constructed DbContext
        var sp = _fixture.CreateServiceProvider(tenantId);
        using (var scope = sp.CreateScope())
        {
            var dbTenantContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            var fetchedUser = await dbTenantContext.Users.FirstAsync(u => u.Id == user.Id);
            fetchedUser.ChangeRole(newRole.Id, tenantId);
            await dbTenantContext.SaveChangesAsync();
        }

        // 3. Assert
        using (var dbVerify = _fixture.CreateDbContext(tenantId))
        {
            var messages = await dbVerify.OutboxMessages
                .Where(o => o.Type.Contains(nameof(UserRoleChangedIntegrationEventV1)))
                .ToListAsync();

            var outboxMessage = Assert.Single(messages);
            Assert.Equal(tenantId, outboxMessage.TenantId);
        }
    }

    [Fact]
    public async Task ChangeRole_WithoutRealChange_ShouldNotGenerateOutboxMessage()
    {
        Guid tenantId = Guid.NewGuid();
        User user;
        Role role;

        // 1. Arrange
        using (var dbSetup = _fixture.CreateDbContext(tenantId))
        {
            role = Role.Create(tenantId, "InitialRole", "Desc");
            dbSetup.Roles.Add(role);
            user = User.Create(new Email("noop@tenant.com"), new PasswordHash("hash"), role.Id, tenantId, "Test", "User");
            dbSetup.Users.Add(user);
            await dbSetup.SaveChangesAsync();
            
            // Delete any outbox messages generated by Create
            var existingMessages = await dbSetup.OutboxMessages.ToListAsync();
            dbSetup.OutboxMessages.RemoveRange(existingMessages);
            await dbSetup.SaveChangesAsync();
        }

        // 2. Act: Attempt to change to the same role and tenant
        using (var dbTenantContext = _fixture.CreateDbContext(tenantId))
        {
            var fetchedUser = await dbTenantContext.Users.FirstAsync(u => u.Id == user.Id);
            fetchedUser.ChangeRole(role.Id, tenantId); // Should be No-Op
            await dbTenantContext.SaveChangesAsync();
        }

        // 3. Assert
        using (var dbVerify = _fixture.CreateDbContext(tenantId))
        {
            var messages = await dbVerify.OutboxMessages.ToListAsync();
            Assert.Empty(messages);
        }
    }

    [Fact]
    public async Task ChangeRole_MoveUserToAnotherTenant_ShouldGenerateOutboxMessageWithNewTenantId()
    {
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();
        User user;
        Role roleA;
        Role roleB;

        // 1. Arrange: Insert roles in Tenant A and Tenant B, and user in Tenant A
        using (var dbSetupA = _fixture.CreateDbContext(tenantA))
        {
            roleA = Role.Create(tenantA, "RoleA", "Desc");
            dbSetupA.Roles.Add(roleA);
            user = User.Create(new Email("move@tenant.com"), new PasswordHash("hash"), roleA.Id, tenantA, "Test", "User");
            dbSetupA.Users.Add(user);
            await dbSetupA.SaveChangesAsync();
        }

        using (var dbSetupB = _fixture.CreateDbContext(tenantB))
        {
            roleB = Role.Create(tenantB, "RoleB", "Desc");
            dbSetupB.Roles.Add(roleB);
            await dbSetupB.SaveChangesAsync();
        }

        // 2. Act: SuperAdmin moves user from Tenant A to Tenant B
        using (var dbTenantA = _fixture.CreateDbContext(tenantA))
        {
            user = await dbTenantA.Users.FirstAsync(u => u.Id == user.Id);
        }

        using (var dbSuperAdmin = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            dbSuperAdmin.Users.Attach(user);
            user.ChangeRole(roleB.Id, tenantB);
            await dbSuperAdmin.SaveChangesAsync();
        }

        // 3. Assert: Verify the outbox message is generated in Tenant B's outbox
        using (var dbVerifyB = _fixture.CreateDbContext(tenantB))
        {
            var messages = await dbVerifyB.OutboxMessages
                .Where(o => o.Type.Contains(nameof(UserRoleChangedIntegrationEventV1)))
                .ToListAsync();

            var outboxMessage = Assert.Single(messages);
            Assert.Equal(tenantB, outboxMessage.TenantId);

            var integrationEvent = JsonSerializer.Deserialize<UserRoleChangedIntegrationEventV1>(outboxMessage.Content, _jsonOptions);
            Assert.NotNull(integrationEvent);
            Assert.Equal(user.Id, integrationEvent.UserId);
            Assert.Equal(roleA.Id, integrationEvent.OldRoleId);
            Assert.Equal(roleB.Id, integrationEvent.NewRoleId);
            Assert.Equal(tenantA, integrationEvent.OldTenantId);
            Assert.Equal(tenantB, integrationEvent.NewTenantId);
        }
    }

    [Fact]
    public async Task Deactivate_WithTenantContext_ShouldGenerateOutboxMessageWithThatTenantId()
    {
        Guid tenantId = Guid.NewGuid();
        User user;

        // 1. Arrange
        using (var dbSetup = _fixture.CreateDbContext(tenantId))
        {
            var role = Role.Create(tenantId, "Role", "Desc");
            dbSetup.Roles.Add(role);
            user = User.Create(new Email("status@tenant.com"), new PasswordHash("hash"), role.Id, tenantId, "Test", "User");
            dbSetup.Users.Add(user);
            await dbSetup.SaveChangesAsync();
        }

        // 2. Act
        var sp = _fixture.CreateServiceProvider(tenantId);
        using (var scope = sp.CreateScope())
        {
            var dbTenantContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            var fetchedUser = await dbTenantContext.Users.FirstAsync(u => u.Id == user.Id);
            fetchedUser.Deactivate();
            await dbTenantContext.SaveChangesAsync();
        }

        // 3. Assert
        using (var dbVerify = _fixture.CreateDbContext(tenantId))
        {
            var messages = await dbVerify.OutboxMessages
                .Where(o => o.Type.Contains(nameof(UserStatusChangedIntegrationEventV1)))
                .ToListAsync();

            var outboxMessage = Assert.Single(messages);
            Assert.Equal(tenantId, outboxMessage.TenantId);

            var integrationEvent = JsonSerializer.Deserialize<UserStatusChangedIntegrationEventV1>(outboxMessage.Content, _jsonOptions);
            Assert.NotNull(integrationEvent);
            Assert.Equal(user.Id, integrationEvent.UserId);
            Assert.Equal(tenantId, integrationEvent.TenantId);
            Assert.False(integrationEvent.IsActive);
        }
    }

    [Fact]
    public async Task Deactivate_WithSuperAdminContext_ShouldGenerateOutboxMessageWithUserTenantId()
    {
        Guid tenantId = Guid.NewGuid();
        User user;

        // 1. Arrange
        using (var dbSetup = _fixture.CreateDbContext(tenantId))
        {
            var role = Role.Create(tenantId, "Role", "Desc");
            dbSetup.Roles.Add(role);
            user = User.Create(new Email("superstatus@tenant.com"), new PasswordHash("hash"), role.Id, tenantId, "Test", "User");
            dbSetup.Users.Add(user);
            await dbSetup.SaveChangesAsync();
        }

        // 2. Act
        using (var dbTenantContext = _fixture.CreateDbContext(tenantId))
        {
            user = await dbTenantContext.Users.FirstAsync(u => u.Id == user.Id);
        }

        using (var dbSuperAdmin = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            dbSuperAdmin.Users.Attach(user);
            user.Deactivate();
            await dbSuperAdmin.SaveChangesAsync();
        }

        // 3. Assert
        using (var dbVerify = _fixture.CreateDbContext(tenantId))
        {
            var messages = await dbVerify.OutboxMessages
                .Where(o => o.Type.Contains(nameof(UserStatusChangedIntegrationEventV1)))
                .ToListAsync();

            var outboxMessage = Assert.Single(messages);
            Assert.Equal(tenantId, outboxMessage.TenantId);

            var integrationEvent = JsonSerializer.Deserialize<UserStatusChangedIntegrationEventV1>(outboxMessage.Content, _jsonOptions);
            Assert.NotNull(integrationEvent);
            Assert.Equal(user.Id, integrationEvent.UserId);
            Assert.Equal(tenantId, integrationEvent.TenantId);
            Assert.False(integrationEvent.IsActive);
        }
    }
}
