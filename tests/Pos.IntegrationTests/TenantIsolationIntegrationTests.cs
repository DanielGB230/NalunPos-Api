using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Persistence.Context;
using Pos.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Pos.IntegrationTests;

[Collection("IntegrationTests")]
public class TenantIsolationIntegrationTests
{
    private readonly MsSqlTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public TenantIsolationIntegrationTests(MsSqlTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task TenantIsolation_TenantAUser_MustNeverSeeTenantBData_ForCategoriesAndProducts()
    {
        // 1. Arrange: Create two tenants and seed data using DbContexts bound to each tenant
        Guid tenantAId = Guid.NewGuid();
        Guid tenantBId = Guid.NewGuid();

        // Seed Tenant A Data
        using (var dbA = _fixture.CreateDbContext(tenantAId))
        {
            var catA1 = Category.Create("Cat A1 Active", "Active cat for Tenant A");
            var catA2 = Category.Create("Cat A2 Inactive", "Inactive cat for Tenant A");
            catA2.Deactivate();

            var prodA1 = Product.Create("Prod A1 Active", Sku.Create("SKU-A1"), Money.Create(10.0m), catA1.Id);
            var prodA2 = Product.Create("Prod A2 Inactive", Sku.Create("SKU-A2"), Money.Create(20.0m), catA1.Id);
            prodA2.Deactivate();

            dbA.Categories.AddRange(catA1, catA2);
            dbA.Products.AddRange(prodA1, prodA2);
            await dbA.SaveChangesAsync();
        }

        // Seed Tenant B Data
        using (var dbB = _fixture.CreateDbContext(tenantBId))
        {
            var catB1 = Category.Create("Cat B1 Active", "Active cat for Tenant B");
            var catB2 = Category.Create("Cat B2 Inactive", "Inactive cat for Tenant B");
            catB2.Deactivate();

            var prodB1 = Product.Create("Prod B1 Active", Sku.Create("SKU-B1"), Money.Create(30.0m), catB1.Id);
            var prodB2 = Product.Create("Prod B2 Inactive", Sku.Create("SKU-B2"), Money.Create(40.0m), catB1.Id);
            prodB2.Deactivate();

            dbB.Categories.AddRange(catB1, catB2);
            dbB.Products.AddRange(prodB1, prodB2);
            await dbB.SaveChangesAsync();
        }

        // 2. Act & Assert: Query repositories as Tenant A User
        var spA = _fixture.CreateServiceProvider(tenantAId);
        using var scopeA = spA.CreateScope();
        var catRepo = scopeA.ServiceProvider.GetRequiredService<ICategoryRepository>();
        var prodRepo = scopeA.ServiceProvider.GetRequiredService<IProductRepository>();

        // Case 1: isActive = null (Fetch ALL items of tenant)
        var (categoriesNull, _) = await catRepo.GetPagedAsync(1, 100, null, isActive: null);
        var (productsNull, _) = await prodRepo.GetPagedAsync(1, 100, null, null, isActive: null);

        _output.WriteLine($"[isActive = null] Categories retrieved: {categoriesNull.Count}, Products retrieved: {productsNull.Count}");
        foreach (var c in categoriesNull) _output.WriteLine($"  Cat: {c.Name}, TenantId: {c.TenantId}");

        // ASSERT: Must NOT contain any items belonging to Tenant B, must contain 2 items for Tenant A
        Assert.Equal(2, categoriesNull.Count);
        Assert.All(categoriesNull, c => Assert.Equal(tenantAId, c.TenantId));
        Assert.Equal(2, productsNull.Count);
        Assert.All(productsNull, p => Assert.Equal(tenantAId, p.TenantId));

        // Case 2: isActive = true (Fetch active items only)
        var (categoriesTrue, _) = await catRepo.GetPagedAsync(1, 100, null, isActive: true);
        var (productsTrue, _) = await prodRepo.GetPagedAsync(1, 100, null, null, isActive: true);

        Assert.Single(categoriesTrue);
        Assert.All(categoriesTrue, c => { Assert.Equal(tenantAId, c.TenantId); Assert.True(c.IsActive); });
        Assert.Single(productsTrue);
        Assert.All(productsTrue, p => { Assert.Equal(tenantAId, p.TenantId); Assert.True(p.IsActive); });

        // Case 3: isActive = false (Fetch inactive items only)
        var (categoriesFalse, _) = await catRepo.GetPagedAsync(1, 100, null, isActive: false);
        var (productsFalse, _) = await prodRepo.GetPagedAsync(1, 100, null, null, isActive: false);

        Assert.Single(categoriesFalse);
        Assert.All(categoriesFalse, c => { Assert.Equal(tenantAId, c.TenantId); Assert.False(c.IsActive); });
        Assert.Single(productsFalse);
        Assert.All(productsFalse, p => { Assert.Equal(tenantAId, p.TenantId); Assert.False(p.IsActive); });
    }

    [Fact]
    public async Task TenantIsolation_UnresolvedTenantContext_MustReturnZeroRows_FailClosed()
    {
        // Arrange: DbContext without resolved tenant (currentTenantId = null)
        using var dbUnresolved = _fixture.CreateDbContext(tenantId: null);

        // Act: Query categories and products directly on unresolved context
        var categories = await dbUnresolved.Categories.ToListAsync();
        var products = await dbUnresolved.Products.ToListAsync();

        _output.WriteLine($"[Unresolved Context] Categories: {categories.Count}, Products: {products.Count}");

        // Assert FAIL-CLOSED: Must return 0 rows when tenant is unresolved!
        Assert.Empty(categories);
        Assert.Empty(products);
    }

    [Fact]
    public async Task TenantIsolation_TenantAUser_MustNeverSeeOrModifyTenantBUsers()
    {
        // 1. Arrange: Create two tenants and seed users for each tenant + 1 SuperAdmin
        Guid tenantAId = Guid.NewGuid();
        Guid tenantBId = Guid.NewGuid();

        var userA = User.Create("userA@tenantA.com", "hashA", Pos.Domain.Enums.UserRole.TenantAdmin, tenantAId, "User", "A");
        var userB = User.Create("userB@tenantB.com", "hashB", Pos.Domain.Enums.UserRole.TenantAdmin, tenantBId, "User", "B");
        var superAdmin = User.Create("superadmin@platform.com", "hashSuper", Pos.Domain.Enums.UserRole.SuperAdmin, null, "Super", "Admin");

        using (var dbA = _fixture.CreateDbContext(tenantAId))
        {
            dbA.Users.Add(userA);
            await dbA.SaveChangesAsync();
        }

        using (var dbB = _fixture.CreateDbContext(tenantBId))
        {
            dbB.Users.AddRange(userB, superAdmin);
            await dbB.SaveChangesAsync();
        }

        // 2. Act & Assert: Query UserRepository as Tenant A
        var spA = _fixture.CreateServiceProvider(tenantAId);
        using var scopeA = spA.CreateScope();
        var userRepo = scopeA.ServiceProvider.GetRequiredService<IUserRepository>();

        // Case 1: GetPagedAsync must return ONLY userA (1 item), NEVER userB or superAdmin
        var (pagedUsers, count) = await userRepo.GetPagedAsync(1, 100, null, isActive: null);
        _output.WriteLine($"[User Isolation] Tenant A retrieved {pagedUsers.Count} users. Total: {count}");
        foreach (var u in pagedUsers) _output.WriteLine($"  User: {u.Email.Value}, TenantId: {u.TenantId}");

        Assert.Single(pagedUsers);
        Assert.Equal(userA.Id, pagedUsers[0].Id);
        Assert.Equal(tenantAId, pagedUsers[0].TenantId);

        // Case 2: GetByIdAsync for Tenant B user must return NULL
        var fetchedUserB = await userRepo.GetByIdAsync(userB.Id);
        Assert.Null(fetchedUserB);

        // Case 3: GetByIdAsync for SuperAdmin user must return NULL
        var fetchedSuperAdmin = await userRepo.GetByIdAsync(superAdmin.Id);
        Assert.Null(fetchedSuperAdmin);
    }

    [Fact]
    public async Task CreateUser_WithEmailAlreadyUsedInAnotherTenant_ShouldReturnConflict()
    {
        Guid tenantAId = Guid.NewGuid();
        Guid tenantBId = Guid.NewGuid();
        string sharedEmail = "shared@platform.com";

        // Create user in Tenant A
        using (var dbA = _fixture.CreateDbContext(tenantAId))
        {
            var userA = User.Create(sharedEmail, "hashA", Pos.Domain.Enums.UserRole.TenantAdmin, tenantAId, "User", "A");
            dbA.Users.Add(userA);
            await dbA.SaveChangesAsync();
        }

        // Try to create user in Tenant B via CommandHandler
        var spB = _fixture.CreateServiceProvider(tenantBId);
        using var scopeB = spB.CreateScope();
        var dispatcher = scopeB.ServiceProvider.GetRequiredService<Pos.Application.Common.Interfaces.IDispatcher>();

        var command = new Pos.Application.Users.Commands.CreateUserCommand(
            "User", "B", sharedEmail, "Password123!", Pos.Domain.Enums.UserRole.Cajero, tenantBId);

        var result = await dispatcher.SendAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal(Pos.Domain.Common.ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public async Task OutboxProcessor_TenantContext_IsResolvedForEventHandler()
    {
        // 1. Arrange
        Guid tenantId = Guid.NewGuid();
        DummyTenantIntegrationEventHandler.Reset();

        using (var db = _fixture.CreateDbContext(tenantId))
        {
            var cat = Pos.Domain.Entities.Category.Create("Test Cat", "Desc");
            db.Categories.Add(cat);

            var dummyEvent = new DummyTenantIntegrationEvent();
            string json = System.Text.Json.JsonSerializer.Serialize(dummyEvent);
            var message = Pos.Infrastructure.Persistence.Outbox.OutboxMessage.Create(
                dummyEvent.Id,
                tenantId,
                typeof(DummyTenantIntegrationEvent).AssemblyQualifiedName!,
                json,
                dummyEvent.OccurredOnUtc
            );
            
            db.OutboxMessages.Add(message);
            await db.SaveChangesAsync();
        }

        var sp = _fixture.CreateServiceProvider(null); // No HTTP context for BackgroundService

        // We use Reflection to call the private method ProcessOutboxMessagesAsync
        var outboxProcessor = new Pos.Api.BackgroundServices.OutboxProcessorBackgroundService(
            sp.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Pos.Api.BackgroundServices.OutboxProcessorBackgroundService>>(),
            Microsoft.Extensions.Options.Options.Create(new Pos.Api.BackgroundServices.OutboxSettings { PollingIntervalSeconds = 1 })
        );

        var methodInfo = typeof(Pos.Api.BackgroundServices.OutboxProcessorBackgroundService)
            .GetMethod("ProcessOutboxMessagesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // 2. Act
        await (Task)methodInfo!.Invoke(outboxProcessor, new object[] { CancellationToken.None })!;

        // 3. Assert
        Assert.Equal(tenantId, DummyTenantIntegrationEventHandler.LastResolvedTenantId);
        Assert.True(DummyTenantIntegrationEventHandler.HitCount > 0);
    }
}

public class DummyTenantIntegrationEvent : Pos.Application.IntegrationEvents.Contracts.IIntegrationEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public class DummyTenantIntegrationEventHandler : Pos.Application.Common.Interfaces.IIntegrationEventHandler<DummyTenantIntegrationEvent>
{
    public static Guid? LastResolvedTenantId { get; private set; }
    public static int HitCount { get; private set; }

    private readonly Pos.Application.Common.Interfaces.ICurrentTenantContext _tenantContext;
    private readonly Pos.Domain.Interfaces.ICategoryRepository _categoryRepository;

    public DummyTenantIntegrationEventHandler(
        Pos.Application.Common.Interfaces.ICurrentTenantContext tenantContext,
        Pos.Domain.Interfaces.ICategoryRepository categoryRepository)
    {
        _tenantContext = tenantContext;
        _categoryRepository = categoryRepository;
    }

    public async Task HandleAsync(DummyTenantIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        LastResolvedTenantId = _tenantContext.TenantId;
        // Verify we can read data for this tenant
        var (cats, _) = await _categoryRepository.GetPagedAsync(1, 10, null, null, cancellationToken);
        HitCount += cats.Count; // Will be > 0 if fail-closed is bypassed properly
    }

    public static void Reset()
    {
        LastResolvedTenantId = null;
        HitCount = 0;
    }
}
