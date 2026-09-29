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

    [Fact]
    public async Task SuperAdminContext_ActivatingAndSuspendingTenants_ShouldGenerateOutboxMessagesWithRespectiveTenantIds()
    {
        // 1. Arrange
        var tenant1 = Tenant.Create("Tenant 1", "TAX1");
        var tenant2 = Tenant.Create("Tenant 2", "TAX2");
        tenant2.Activate(); // For suspending later
        tenant1.ClearDomainEvents();
        tenant2.ClearDomainEvents();

        using (var dbSetup = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            dbSetup.Tenants.Add(tenant1);
            dbSetup.Tenants.Add(tenant2);
            await dbSetup.SaveChangesAsync();
        }

        // 2. Act
        using (var dbSuperAdmin = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            tenant1 = await dbSuperAdmin.Tenants.FirstAsync(t => t.Id == tenant1.Id);
            tenant2 = await dbSuperAdmin.Tenants.FirstAsync(t => t.Id == tenant2.Id);

            tenant1.Activate();
            tenant2.Suspend();

            await dbSuperAdmin.SaveChangesAsync();
        }

        // 3. Assert
        using (var dbVerify = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            var messagesT1 = await dbVerify.OutboxMessages
                .Where(o => o.TenantId == tenant1.Id && o.Type.Contains(nameof(TenantStatusChangedIntegrationEventV1)))
                .ToListAsync();
            
            var outboxT1 = Assert.Single(messagesT1);
            var eventT1 = JsonSerializer.Deserialize<TenantStatusChangedIntegrationEventV1>(outboxT1.Content, _jsonOptions);
            Assert.NotNull(eventT1);
            Assert.Equal("PendingProvisioning", eventT1.OldStatus);
            Assert.Equal("Active", eventT1.NewStatus);
            Assert.Equal(tenant1.Id, eventT1.TenantId);

            var messagesT2 = await dbVerify.OutboxMessages
                .Where(o => o.TenantId == tenant2.Id && o.Type.Contains(nameof(TenantStatusChangedIntegrationEventV1)))
                .ToListAsync();
            
            var outboxT2 = Assert.Single(messagesT2);
            var eventT2 = JsonSerializer.Deserialize<TenantStatusChangedIntegrationEventV1>(outboxT2.Content, _jsonOptions);
            Assert.NotNull(eventT2);
            Assert.Equal("Active", eventT2.OldStatus);
            Assert.Equal("Suspended", eventT2.NewStatus);
            Assert.Equal(tenant2.Id, eventT2.TenantId);
        }
    }

    [Fact]
    public async Task EventTenantId_ShouldWinOverContextTenantId_WhenActivatingTenant()
    {
        // 1. Arrange
        Guid wrongContextTenantId = Guid.NewGuid();
        var targetTenant = Tenant.Create("Target Tenant", "TAX-TARGET");
        targetTenant.ClearDomainEvents();

        using (var dbSetup = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            dbSetup.Tenants.Add(targetTenant);
            await dbSetup.SaveChangesAsync();
        }

        // 2. Act
        // Use a context with a different TenantId, but IsSuperAdmin = true so it can read/write the Tenant
        using (var dbAdmin = _fixture.CreateDbContext(tenantId: wrongContextTenantId, isSuperAdmin: true))
        {
            targetTenant = await dbAdmin.Tenants.FirstAsync(t => t.Id == targetTenant.Id);
            targetTenant.Activate();
            await dbAdmin.SaveChangesAsync();
        }

        // 3. Assert
        using (var dbVerify = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            // Verify message has targetTenant.Id, NOT wrongContextTenantId
            var messages = await dbVerify.OutboxMessages
                .Where(o => o.Type.Contains(nameof(TenantStatusChangedIntegrationEventV1)) && o.TenantId == targetTenant.Id)
                .ToListAsync();
            
            var outboxMessage = Assert.Single(messages);
            Assert.Equal(targetTenant.Id, outboxMessage.TenantId);
            Assert.NotEqual(wrongContextTenantId, outboxMessage.TenantId);
        }
    }

    [Fact]
    public async Task DeactivateProduct_WithTenantContext_ShouldGenerateOutboxMessageWithThatTenantId()
    {
        Guid tenantId = Guid.NewGuid();
        Product product;

        // 1. Arrange
        using (var dbSetup = _fixture.CreateDbContext(tenantId))
        {
            var category = Category.Create(tenantId, "Electrónica", "Desc");
            dbSetup.Categories.Add(category);
            
            product = Product.Create("Producto", Sku.Create("SKU-1"), Money.Create(10, "USD"), category.Id);
            dbSetup.Products.Add(product);
            
            await dbSetup.SaveChangesAsync();
        }

        // 2. Act
        using (var dbTenantContext = _fixture.CreateDbContext(tenantId))
        {
            var fetchedProduct = await dbTenantContext.Products.FirstAsync(p => p.Id == product.Id);
            fetchedProduct.Deactivate();
            await dbTenantContext.SaveChangesAsync();
        }

        // 3. Assert
        using (var dbVerify = _fixture.CreateDbContext(tenantId))
        {
            var messages = await dbVerify.OutboxMessages
                .Where(o => o.Type.Contains(nameof(ProductStatusChangedIntegrationEventV1)))
                .ToListAsync();

            var outboxMessage = Assert.Single(messages);
            Assert.Equal(tenantId, outboxMessage.TenantId);

            var integrationEvent = JsonSerializer.Deserialize<ProductStatusChangedIntegrationEventV1>(outboxMessage.Content, _jsonOptions);
            Assert.NotNull(integrationEvent);
            Assert.Equal(product.Id, integrationEvent.ProductId);
            Assert.Equal(tenantId, integrationEvent.TenantId);
            Assert.False(integrationEvent.IsActive);
        }
    }

    [Fact]
    public async Task DeactivateProduct_WithSuperAdminContext_ShouldGenerateOutboxMessageWithProductTenantId()
    {
        Guid tenantId = Guid.NewGuid();
        Product product;

        // 1. Arrange
        using (var dbSetup = _fixture.CreateDbContext(tenantId))
        {
            var category = Category.Create(tenantId, "Electrónica", "Desc");
            dbSetup.Categories.Add(category);
            
            product = Product.Create("Producto Admin", Sku.Create("SKU-ADM"), Money.Create(10, "USD"), category.Id);
            dbSetup.Products.Add(product);
            
            await dbSetup.SaveChangesAsync();
        }

        // 2. Act
        using (var dbTenantContext = _fixture.CreateDbContext(tenantId))
        {
            product = await dbTenantContext.Products.FirstAsync(p => p.Id == product.Id);
        }

        using (var dbSuperAdmin = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            dbSuperAdmin.Products.Attach(product);
            product.Deactivate();
            await dbSuperAdmin.SaveChangesAsync();
        }

        // 3. Assert
        using (var dbVerify = _fixture.CreateDbContext(tenantId))
        {
            var messages = await dbVerify.OutboxMessages
                .Where(o => o.Type.Contains(nameof(ProductStatusChangedIntegrationEventV1)))
                .ToListAsync();

            var outboxMessage = Assert.Single(messages);
            Assert.Equal(tenantId, outboxMessage.TenantId);

            var integrationEvent = JsonSerializer.Deserialize<ProductStatusChangedIntegrationEventV1>(outboxMessage.Content, _jsonOptions);
            Assert.NotNull(integrationEvent);
            Assert.Equal(product.Id, integrationEvent.ProductId);
            Assert.Equal(tenantId, integrationEvent.TenantId);
            Assert.False(integrationEvent.IsActive);
        }
    }
}
