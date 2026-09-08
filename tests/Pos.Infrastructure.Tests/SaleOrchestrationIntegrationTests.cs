using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pos.Application;
using Pos.Application.CashRegisters.Commands;
using Pos.Application.Common.Interfaces;
using Pos.Application.Inventory.Commands;
using Pos.Application.Payments.Commands;
using Pos.Application.Sales.Commands;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.ExternalServices.Dummy;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Pos.Infrastructure.Tests;

public class SaleOrchestrationIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IServiceProvider _serviceProvider;

    public SaleOrchestrationIntegrationTests()
    {
        var services = new ServiceCollection();

        // 0. Logging
        services.AddLogging();

        // 1. Base de datos EF Core en memoria real usando SQLite (Soporta ComplexProperty de .NET 10)
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        services.AddDbContext<PosDbContext>(options =>
            options.UseSqlite(_connection));

        // 2. Registro de UnitOfWork e Infraestructura real
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PosDbContext>());
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<ICashRegisterRepository, CashRegisterRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();

        // 3. Capa Anti-Corrupción (ACL) para Pasarela de Pagos (Dummy)
        services.AddScoped<IPaymentGateway, DummyPaymentGateway>();

        // 4. Servicios de aplicación (Dispatcher CQRS, Handlers y DomainEventHandlers)
        services.AddApplicationServices();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task ExecuteEndToEndSaleOrchestrationFlow_ShouldProcessSale_UpdateStock_AndMarkAsPaid()
    {
        using var scope = _serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var dbContext = sp.GetRequiredService<PosDbContext>();
        var inventoryRepo = sp.GetRequiredService<IInventoryRepository>();

        await dbContext.Database.EnsureCreatedAsync();

        // ==========================================
        // PASO 1: SEMILLA — Crear Categoría y Producto con 10 unidades de stock inicial
        // ==========================================
        var category = Category.Create("Bebidas", "Categoría de bebidas");
        dbContext.Categories.Add(category);

        var product = Product.Create(
            "Gaseosa Cola 500ml",
            Sku.Create("BEB-COL-500"),
            Money.Create(3.50m, "USD"),
            category.Id,
            barcode: Barcode.Create("7751234567890"),
            cost: Money.Create(2.00m, "USD"),
            initialStock: 0
        );
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        // Registrar movimiento inicial en Kardex para reflejar las 10 unidades en la BD
        var initialStockResult = await dispatcher.SendAsync(new RecordInventoryMovementCommand(
            product.Id,
            10,
            InventoryMovementType.Adjustment,
            Notes: "Stock Inicial de Prueba"
        ));
        Assert.True(initialStockResult.IsSuccess);

        // Verificar stock inicial en BD (Producto = 10, Kardex = 10)
        decimal initialKardexStock = await inventoryRepo.GetCurrentStockAsync(product.Id);
        Assert.Equal(10, initialKardexStock);

        // ==========================================
        // PASO 2: CAJA — Crear Sucursal, Caja y Abrir Sesión de Caja
        // ==========================================
        var address = Address.Create("Av. Central 123", "Lima", "15001", "PE");
        var branch = Branch.Create("Sucursal Principal", address, "+511999888777");
        dbContext.Branches.Add(branch);

        var register = CashRegister.Create(branch.Id, "Caja 01", "CR-001");
        dbContext.CashRegisters.Add(register);
        await dbContext.SaveChangesAsync();

        var userId = Guid.NewGuid();
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
        var sessionInDb = await dbContext.CashRegisterSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        Assert.NotNull(sessionInDb);
        Assert.Equal(SessionStatus.Open, sessionInDb.Status);

        // ==========================================
        // PASO 3: VENTA — Ejecutar CreateSaleCommand comprando 2 unidades del producto
        // ==========================================
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

        // ==========================================
        // VERIFICACIÓN DE INVENTARIO (CRUCIAL):
        // Confirmar que el stock bajó automáticamente a 8 por la orquestación del evento de dominio
        // (SaleCompletedDomainEvent -> RecordInventoryOnSaleCompletedHandler -> RecordInventoryMovementCommand)
        // ==========================================
        var productInDb = await dbContext.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
        Assert.NotNull(productInDb);
        Assert.Equal(8, productInDb.StockQuantity);

        decimal currentKardexStock = await inventoryRepo.GetCurrentStockAsync(product.Id);
        Assert.Equal(8, currentKardexStock);

        // ==========================================
        // PASO 4: PAGO — Ejecutar ProcessPaymentCommand para la venta creada
        // ==========================================
        var processPaymentCommand = new ProcessPaymentCommand(
            SaleId: saleId,
            Amount: createSaleResult.Value.TotalAmount,
            Method: PaymentMethod.Cash,
            Currency: "USD",
            ExternalReference: "EFECTIVO-001"
        );

        var processPaymentResult = await dispatcher.SendAsync(processPaymentCommand);
        Assert.True(processPaymentResult.IsSuccess);
        Assert.NotNull(processPaymentResult.Value);

        // ==========================================
        // VERIFICACIÓN FINAL:
        // Confirmar que el estado de la Venta cambió a Paid (Pagada)
        // mediante el evento de dominio desacoplado (PaymentProcessedDomainEvent -> MarkSalePaidOnPaymentProcessedHandler)
        // ==========================================
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
