using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.CashRegisters.Commands;
using Pos.Application.Common.Interfaces;
using Pos.Application.Inventory.Commands;
using Pos.Application.Payments.Commands;
using Pos.Application.Sales.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Persistence.Context;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

[Collection("IntegrationTests")]
public class SaleFullIntegrationTests
{
    private readonly MsSqlTestFixture _fixture;

    public SaleFullIntegrationTests(MsSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Scenario1_HappyPath_OpenSession_CreateSale_DeductStock_ProcessPayment_MarkAsPaid()
    {
        // Arrange
        var serviceProvider = _fixture.CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var dbContext = sp.GetRequiredService<PosDbContext>();
        var inventoryRepo = sp.GetRequiredService<IInventoryRepository>();

        // 1. Semilla: Crear Categoría y Producto con 10 unidades de stock
        var category = Category.Create($"Bebidas_{Guid.NewGuid()}", "Categoría de prueba");
        dbContext.Categories.Add(category);

        var product = Product.Create(
            $"Inca Kola 500ml_{Guid.NewGuid()}",
            Sku.Create($"SKU-{Guid.NewGuid().ToString()[..8]}"),
            Money.Create(4.00m, "USD"),
            category.Id,
            barcode: Barcode.Create("7750001000111"),
            cost: Money.Create(2.50m, "USD"),
            initialStock: 0
        );
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var initStockResult = await dispatcher.SendAsync(new RecordInventoryMovementCommand(
            product.Id,
            10,
            InventoryMovementType.Adjustment,
            Notes: "Stock Inicial de Integración"
        ));
        Assert.True(initStockResult.IsSuccess);

        // 2. Caja: Crear Sucursal, Caja y Abrir Sesión de Caja
        var address = Address.Create("Av. Larco 456", "Lima", "15074", "PE");
        var branch = Branch.Create($"Sucursal Sur_{Guid.NewGuid()}", address, "+511987654321");
        dbContext.Branches.Add(branch);

        var register = CashRegister.Create($"Caja Principal_{Guid.NewGuid()}", $"CR-{Guid.NewGuid().ToString()[..6]}");
        dbContext.CashRegisters.Add(register);
        await dbContext.SaveChangesAsync();

        var userId = Guid.NewGuid();
        var openSessionResult = await dispatcher.SendAsync(new OpenCashRegisterSessionCommand(
            register.Id,
            userId,
            InitialAmount: 150m,
            Currency: "USD",
            Notes: "Turno Tarde"
        ));
        Assert.True(openSessionResult.IsSuccess);
        Guid sessionId = openSessionResult.Value.Id;

        // 3. Venta: Ejecutar CreateSaleCommand comprando 2 unidades
        string receiptNumber = $"V-INT-{Guid.NewGuid().ToString()[..8]}";
        var createSaleCommand = new CreateSaleCommand(
            ReceiptNumber: receiptNumber,
            SessionId: sessionId,
            CustomerId: null,
            LineItems: new List<CreateSaleItemDto>
            {
                new(product.Id, product.Name, Quantity: 2, UnitPriceAmount: 4.00m)
            },
            TaxRatePercentage: 18m,
            Currency: "USD"
        );

        // Act - Crear Venta
        var createSaleResult = await dispatcher.SendAsync(createSaleCommand);

        // Assert - Venta exitosa
        Assert.True(createSaleResult.IsSuccess);
        Guid saleId = createSaleResult.Value.Id;

        // VERIFICACION DE INVENTARIO ATÓMICA EN BASE DE DATOS REAL
        var productInDb = await dbContext.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
        Assert.NotNull(productInDb);
        Assert.Equal(8, productInDb.StockQuantity);

        decimal kardexStock = await inventoryRepo.GetCurrentStockAsync(product.Id);
        Assert.Equal(8, kardexStock);

        // 4. Pago: Procesar pago por el total de la venta
        var processPaymentCommand = new ProcessPaymentCommand(
            SaleId: saleId,
            Amount: createSaleResult.Value.TotalAmount,
            Method: PaymentMethod.CreditCard,
            Currency: "USD",
            ExternalReference: "TARJETA-VISA-999"
        );

        var paymentResult = await dispatcher.SendAsync(processPaymentCommand);
        Assert.True(paymentResult.IsSuccess);

        // VERIFICACION FINAL: Venta debe quedar marcada como Paid (Pagada)
        var saleInDb = await dbContext.Sales.AsNoTracking().FirstOrDefaultAsync(s => s.Id == saleId);
        Assert.NotNull(saleInDb);
        Assert.Equal(SaleStatus.Paid, saleInDb.Status);
    }

    [Fact]
    public async Task Scenario2_InsufficientStock_ShouldFail_AndNotCreateSaleOrModifyStock()
    {
        // Arrange
        var serviceProvider = _fixture.CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var dbContext = sp.GetRequiredService<PosDbContext>();

        var category = Category.Create($"Snacks_{Guid.NewGuid()}", "Categoría snacks");
        dbContext.Categories.Add(category);

        var product = Product.Create(
            $"Papas Fritas 100g_{Guid.NewGuid()}",
            Sku.Create($"SKU-{Guid.NewGuid().ToString()[..8]}"),
            Money.Create(2.50m, "USD"),
            category.Id,
            initialStock: 0
        );
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        // Stock inicial de 3 unidades
        await dispatcher.SendAsync(new RecordInventoryMovementCommand(
            product.Id,
            3,
            InventoryMovementType.Adjustment,
            Notes: "Stock inicial 3 unidades"
        ));

        // Abrir Caja
        var address = Address.Create("Calle Real 100", "Lima", "15000", "PE");
        var branch = Branch.Create($"Sucursal Norte_{Guid.NewGuid()}", address, "+51111111111");
        dbContext.Branches.Add(branch);
        var register = CashRegister.Create($"Caja Express_{Guid.NewGuid()}", $"CR-{Guid.NewGuid().ToString()[..6]}");
        dbContext.CashRegisters.Add(register);
        await dbContext.SaveChangesAsync();

        var openSessionResult = await dispatcher.SendAsync(new OpenCashRegisterSessionCommand(register.Id, Guid.NewGuid(), 50m, "USD"));
        Assert.True(openSessionResult.IsSuccess);

        string receiptNumber = $"V-FAIL-{Guid.NewGuid().ToString()[..8]}";
        var createSaleCommand = new CreateSaleCommand(
            ReceiptNumber: receiptNumber,
            SessionId: openSessionResult.Value.Id,
            CustomerId: null,
            LineItems: new List<CreateSaleItemDto>
            {
                new(product.Id, product.Name, Quantity: 10, UnitPriceAmount: 2.50m) // Intenta vender 10 (solo hay 3)
            },
            Currency: "USD"
        );

        // Act - Intentar Venta sin stock suficiente
        var result = await dispatcher.SendAsync(createSaleCommand);

        // Assert - Operación debe fallar de forma controlada con Result.Fail
        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.InsufficientStock", result.Error.Code);

        // VERIFICACION EN BASE DE DATOS REAL: NO debe existir la venta y el stock no se tocó
        var saleInDb = await dbContext.Sales.AsNoTracking().FirstOrDefaultAsync(s => s.ReceiptNumber == receiptNumber);
        Assert.Null(saleInDb);

        var productInDb = await dbContext.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
        Assert.NotNull(productInDb);
        Assert.Equal(3, productInDb.StockQuantity); // Stock intacto de 3 unidades
    }

    [Fact]
    public async Task Scenario3_SaleWithoutOpenSession_ShouldReturnConflictResult_WithoutException()
    {
        // Arrange
        var serviceProvider = _fixture.CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var dbContext = sp.GetRequiredService<PosDbContext>();

        var category = Category.Create($"Limpieza_{Guid.NewGuid()}", "Categoría limpieza");
        dbContext.Categories.Add(category);

        var product = Product.Create(
            $"Detergente 1L_{Guid.NewGuid()}",
            Sku.Create($"SKU-{Guid.NewGuid().ToString()[..8]}"),
            Money.Create(5.00m, "USD"),
            category.Id,
            initialStock: 10
        );
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        // Sesión de caja cerrada
        var address = Address.Create("Av. Marina 200", "Lima", "15088", "PE");
        var branch = Branch.Create($"Sucursal Oeste_{Guid.NewGuid()}", address, "+51122222222");
        dbContext.Branches.Add(branch);
        var register = CashRegister.Create($"Caja 03_{Guid.NewGuid()}", $"CR-{Guid.NewGuid().ToString()[..6]}");
        dbContext.CashRegisters.Add(register);
        await dbContext.SaveChangesAsync();

        var openSessionResult = await dispatcher.SendAsync(new OpenCashRegisterSessionCommand(register.Id, Guid.NewGuid(), 50m, "USD"));
        Guid closedSessionId = openSessionResult.Value.Id;

        // Cerrar la sesión de caja
        var sessionEntity = await dbContext.CashRegisterSessions.FirstAsync(s => s.Id == closedSessionId);
        sessionEntity.Close(Money.Create(50m, "USD"), Money.Create(50m, "USD"), "Cierre de prueba");
        await dbContext.SaveChangesAsync();

        string receiptNumber = $"V-CLOSED-{Guid.NewGuid().ToString()[..8]}";
        var command = new CreateSaleCommand(
            ReceiptNumber: receiptNumber,
            SessionId: closedSessionId,
            CustomerId: null,
            LineItems: new List<CreateSaleItemDto>
            {
                new(product.Id, product.Name, 1, 5.00m)
            }
        );

        // Act
        var result = await dispatcher.SendAsync(command);

        // Assert - Falla con conflicto de negocio controlado (Result.Fail)
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("CashRegisterSession.Closed", result.Error.Code);

        // Verificar que NO se persistió la venta en la BD real
        var saleInDb = await dbContext.Sales.AsNoTracking().FirstOrDefaultAsync(s => s.ReceiptNumber == receiptNumber);
        Assert.Null(saleInDb);
    }

    [Fact]
    public async Task Scenario4_MultiTenantIsolation_TenantASaleAndStock_ShouldNeverAffectTenantB()
    {
        // Arrange
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();

        var spTenantA = _fixture.CreateServiceProvider(tenantA);
        var spTenantB = _fixture.CreateServiceProvider(tenantB);

        // Crear producto para Tenant A
        using var scopeA = spTenantA.CreateScope();
        var dispatcherA = scopeA.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbA = scopeA.ServiceProvider.GetRequiredService<PosDbContext>();

        var catA = Category.Create($"CatA_{Guid.NewGuid()}", "Cat Tenant A");
        dbA.Categories.Add(catA);
        var prodA = Product.Create($"ProdA_{Guid.NewGuid()}", Sku.Create($"SKUA-{Guid.NewGuid().ToString()[..6]}"), Money.Create(10m, "USD"), catA.Id);
        dbA.Products.Add(prodA);
        await dbA.SaveChangesAsync();
        await dispatcherA.SendAsync(new RecordInventoryMovementCommand(prodA.Id, 10, InventoryMovementType.Adjustment));

        // Crear producto para Tenant B
        using var scopeB = spTenantB.CreateScope();
        var dispatcherB = scopeB.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<PosDbContext>();

        var catB = Category.Create($"CatB_{Guid.NewGuid()}", "Cat Tenant B");
        dbB.Categories.Add(catB);
        var prodB = Product.Create($"ProdB_{Guid.NewGuid()}", Sku.Create($"SKUB-{Guid.NewGuid().ToString()[..6]}"), Money.Create(20m, "USD"), catB.Id);
        dbB.Products.Add(prodB);
        await dbB.SaveChangesAsync();
        await dispatcherB.SendAsync(new RecordInventoryMovementCommand(prodB.Id, 20, InventoryMovementType.Adjustment));

        // Abrir Caja y vender 2 unidades en Tenant A
        var addrA = Address.Create("Av. A 123", "Lima", "15001", "PE");
        var branchA = Branch.Create(tenantA, $"BranchA_{Guid.NewGuid()}", addrA, "+511111111");
        dbA.Branches.Add(branchA);
        var regA = CashRegister.Create("Caja A", branchA.Id, $"CRA-{Guid.NewGuid().ToString()[..4]}");
        dbA.CashRegisters.Add(regA);
        await dbA.SaveChangesAsync();

        var openSessionA = await dispatcherA.SendAsync(new OpenCashRegisterSessionCommand(regA.Id, Guid.NewGuid(), 100m, "USD"));
        Assert.True(openSessionA.IsSuccess);
        var saleACommand = new CreateSaleCommand(
            ReceiptNumber: $"VA-{Guid.NewGuid().ToString()[..6]}",
            SessionId: openSessionA.Value.Id,
            CustomerId: null,
            LineItems: new List<CreateSaleItemDto> { new(prodA.Id, prodA.Name, 2, 10m) }
        );

        var saleAResult = await dispatcherA.SendAsync(saleACommand);
        Assert.True(saleAResult.IsSuccess);

        // Assert - Aislamiento Multi-Tenant
        // Tenant A stock bajó a 8
        var prodAInDb = await dbA.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == prodA.Id);
        Assert.NotNull(prodAInDb);
        Assert.Equal(8, prodAInDb.StockQuantity);

        // Tenant B stock se mantiene intacto en 20 y no puede ver ni fue afectado por la venta de Tenant A
        var prodBInDb = await dbB.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == prodB.Id);
        Assert.NotNull(prodBInDb);
        Assert.Equal(20, prodBInDb.StockQuantity);
    }
}
