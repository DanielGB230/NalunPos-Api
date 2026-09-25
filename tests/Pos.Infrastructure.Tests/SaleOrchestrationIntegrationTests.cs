using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pos.Application;
using Pos.Application.CashRegisters.Commands;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Authorization;
using Pos.Application.Inventory.Commands;
using Pos.Application.Payments.Commands;
using Pos.Application.Sales.Commands;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.ExternalServices.Dummy;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Multitenancy;
using Pos.Infrastructure.Persistence.Interceptors;
using Pos.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Pos.Infrastructure.Tests;

public class TestTenantContext : ICurrentTenantContext
{
    public Guid? TenantId { get; set; }
    public bool IsSuperAdmin => false;
    public bool HasTenant => TenantId.HasValue;
}

public class MockCurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
}

public class SaleOrchestrationIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IServiceProvider _serviceProvider;
    private readonly TestTenantContext _tenantContext = new();
    private readonly MockCurrentUserService _mockUserService = new();

    public SaleOrchestrationIntegrationTests()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        services.AddScoped<TenantSaveChangesInterceptor>();
        services.AddDbContext<PosDbContext>((sp, options) =>
        {
            options.UseSqlite(_connection);
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });
        services.AddScoped<PosDbContext>(sp =>
        {
            var options = sp.GetRequiredService<DbContextOptions<PosDbContext>>();
            var tenantInterceptor = sp.GetRequiredService<TenantSaveChangesInterceptor>();
            var tenantContext = sp.GetService<ICurrentTenantContext>();
            return new PosDbContext(options, tenantInterceptor: tenantInterceptor, currentTenantId: tenantContext?.TenantId);
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PosDbContext>());
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<IStockLevelRepository, StockLevelRepository>();
        services.AddScoped<ICashRegisterRepository, CashRegisterRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ICurrentUserPermissions, Pos.Infrastructure.Authentication.CurrentUserPermissions>();

        services.AddSingleton<ICurrentTenantContext>(_tenantContext);
        services.AddSingleton<ICurrentUserService>(_mockUserService);
        services.AddScoped<IPaymentGateway, DummyPaymentGateway>();

        services.AddMemoryCache();
#pragma warning disable EXTEXP0018
        services.AddHybridCache();
#pragma warning restore EXTEXP0018

        services.AddApplicationServices();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task ExecuteEndToEndSaleOrchestrationFlow_ShouldProcessSale_UpdateStock_AndMarkAsPaid()
    {
        using var scope = _serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        Guid tenantId = Guid.NewGuid();
        _tenantContext.TenantId = tenantId;

        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var dbContext = sp.GetRequiredService<PosDbContext>();
        var stockLevelRepo = sp.GetRequiredService<IStockLevelRepository>();

        await dbContext.Database.EnsureCreatedAsync();

        // 0. Sembrar Rol y Usuario para la autorización
        var logger = sp.GetRequiredService<ILogger<Pos.Infrastructure.Persistence.Seed.DefaultRoleSeeder>>();
        var seeder = new Pos.Infrastructure.Persistence.Seed.DefaultRoleSeeder(dbContext, logger);
        await seeder.SeedRolesForTenantAsync(tenantId);
        var adminRole = dbContext.Roles.First(r => r.TenantId == tenantId && r.Name == "TenantAdmin");

        var activeUser = User.Create(new Email("activeadmin@test.com"), new PasswordHash("passhash"), adminRole.Id, tenantId, "Active", "Admin");
        dbContext.Users.Add(activeUser);
        await dbContext.SaveChangesAsync();

        _mockUserService.UserId = activeUser.Id;
        _mockUserService.UserEmail = activeUser.Email.Value;

        // 1. SEMILLA — Crear Sucursal y Almacén por defecto
        var address = Address.Create("Av. Central 123", "Lima", "15001", "PE");
        var branch = Branch.Create(tenantId, "Sucursal Principal", address, "+511999888777");
        dbContext.Branches.Add(branch);

        var warehouse = Warehouse.Create(tenantId, branch.Id, "Almacén Principal", isDefault: true);
        dbContext.Warehouses.Add(warehouse);

        var register = CashRegister.Create(tenantId, branch.Id, "Caja 01", "CR-001");
        dbContext.CashRegisters.Add(register);

        var category = Category.Create("Bebidas", "Categoría de bebidas");
        dbContext.Categories.Add(category);

        var product = Product.Create(
            "Gaseosa Cola 500ml",
            Sku.Create("BEB-COL-500"),
            Money.Create(3.50m, "USD"),
            category.Id,
            barcode: Barcode.Create("7751234567890"),
            cost: Money.Create(2.00m, "USD")
        );
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        // Registrar movimiento inicial de 10 unidades en el almacén
        var initialStockResult = await dispatcher.SendAsync(new RecordInventoryMovementCommand(
            product.Id,
            warehouse.Id,
            Quantity: 10m,
            MovementType: InventoryMovementType.Adjustment,
            Notes: "Stock Inicial de Prueba"
        ));
        Assert.True(initialStockResult.IsSuccess, $"Failed: {initialStockResult.Error?.Code} - {initialStockResult.Error?.Message}");

        // Verificar stock disponible en StockLevel
        var initialStockLevel = await stockLevelRepo.GetAsync(product.Id, warehouse.Id, null);
        Assert.NotNull(initialStockLevel);
        Assert.Equal(10m, initialStockLevel.QuantityAvailable);

        // 2. CAJA — Abrir Sesión de Caja
        var userId = activeUser.Id;
        var openSessionResult = await dispatcher.SendAsync(new OpenCashRegisterSessionCommand(
            register.Id,
            userId,
            InitialAmount: 100m,
            Currency: "USD",
            Notes: "Apertura turno mañana"
        ));
        Assert.True(openSessionResult.IsSuccess);
        Assert.NotNull(openSessionResult.Value);

        Guid sessionId = openSessionResult.Value.Id;

        // 3. VENTA — Comprar 2 unidades
        var createSaleCommand = new CreateSaleCommand(
            ReceiptNumber: "V-001-0001",
            SessionId: sessionId,
            CustomerId: null,
            LineItems: new List<CreateSaleItemDto>
            {
                new(product.Id, product.Name, Quantity: 2, UnitPriceAmount: 3.50m)
            },
            TaxRatePercentage: 18m,
            Currency: "USD"
        );

        var createSaleResult = await dispatcher.SendAsync(createSaleCommand);
        Assert.True(createSaleResult.IsSuccess);
        Assert.NotNull(createSaleResult.Value);
        Guid saleId = createSaleResult.Value.Id;

        // VERIFICACIÓN DE STOCK LEVEL (ADR-Inventory-001)
        var updatedStockLevel = await stockLevelRepo.GetAsync(product.Id, warehouse.Id, null);
        Assert.NotNull(updatedStockLevel);
        Assert.Equal(8m, updatedStockLevel.QuantityAvailable);

        // 4. PAGO — Procesar pago
        var processPaymentCommand = new ProcessPaymentCommand(
            SaleId: saleId,
            Amount: createSaleResult.Value.TotalAmount,
            Method: PaymentMethod.Cash,
            Currency: "USD",
            ExternalReference: "EFECTIVO-001"
        );

        var processPaymentResult = await dispatcher.SendAsync(processPaymentCommand);
        Assert.True(processPaymentResult.IsSuccess);

        var saleInDb = await dbContext.Sales.AsNoTracking().FirstOrDefaultAsync(s => s.Id == saleId);
        Assert.NotNull(saleInDb);
        Assert.Equal(SaleStatus.Paid, saleInDb.Status);
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
