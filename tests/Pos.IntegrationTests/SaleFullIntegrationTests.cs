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
        Guid tenantId = Guid.NewGuid();
        var serviceProvider = _fixture.CreateServiceProvider(tenantId);
        using var scope = serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var dbContext = sp.GetRequiredService<PosDbContext>();
        var stockLevelRepo = sp.GetRequiredService<IStockLevelRepository>();

        var address = Address.Create("Av. Larco 456", "Lima", "15074", "PE");
        var branch = Branch.Create(tenantId, $"Sucursal Sur_{Guid.NewGuid()}", address, "+511987654321");
        dbContext.Branches.Add(branch);

        var warehouse = Warehouse.Create(tenantId, branch.Id, "Almacén Principal", isDefault: true);
        dbContext.Warehouses.Add(warehouse);

        var register = CashRegister.Create(tenantId, branch.Id, $"Caja Principal_{Guid.NewGuid()}", $"CR-{Guid.NewGuid().ToString()[..6]}");
        dbContext.CashRegisters.Add(register);

        var category = Category.Create($"Bebidas_{Guid.NewGuid()}", "Categoría de prueba");
        dbContext.Categories.Add(category);

        var product = Product.Create(
            $"Inca Kola 500ml_{Guid.NewGuid()}",
            Sku.Create($"SKU-{Guid.NewGuid().ToString()[..8]}"),
            Money.Create(4.00m, "USD"),
            category.Id,
            barcode: Barcode.Create("7750001000111"),
            cost: Money.Create(2.50m, "USD")
        );
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var initStockResult = await dispatcher.SendAsync(new RecordInventoryMovementCommand(
            product.Id,
            warehouse.Id,
            10m,
            InventoryMovementType.Adjustment,
            Notes: "Stock Inicial de Integración"
        ));
        Assert.True(initStockResult.IsSuccess);

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

        // Act
        var createSaleResult = await dispatcher.SendAsync(createSaleCommand);

        // Assert
        Assert.True(createSaleResult.IsSuccess);
        Guid saleId = createSaleResult.Value.Id;

        // VERIFICACION DE STOCK LEVEL
        var stockLevel = await stockLevelRepo.GetAsync(product.Id, warehouse.Id, null);
        Assert.NotNull(stockLevel);
        Assert.Equal(8m, stockLevel.QuantityAvailable);

        // Pago
        var processPaymentCommand = new ProcessPaymentCommand(
            SaleId: saleId,
            Amount: createSaleResult.Value.TotalAmount,
            Method: PaymentMethod.CreditCard,
            Currency: "USD",
            ExternalReference: "TARJETA-VISA-999"
        );

        var paymentResult = await dispatcher.SendAsync(processPaymentCommand);
        Assert.True(paymentResult.IsSuccess);

        var saleInDb = await dbContext.Sales.AsNoTracking().FirstOrDefaultAsync(s => s.Id == saleId);
        Assert.NotNull(saleInDb);
        Assert.Equal(SaleStatus.Paid, saleInDb.Status);
    }

    [Fact]
    public async Task Scenario2_InsufficientStock_ShouldFail_AndNotCreateSaleOrModifyStock()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        var serviceProvider = _fixture.CreateServiceProvider(tenantId);
        using var scope = serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var dbContext = sp.GetRequiredService<PosDbContext>();
        var stockLevelRepo = sp.GetRequiredService<IStockLevelRepository>();

        var address = Address.Create("Calle Real 100", "Lima", "15000", "PE");
        var branch = Branch.Create(tenantId, $"Sucursal Norte_{Guid.NewGuid()}", address, "+51111111111");
        dbContext.Branches.Add(branch);

        var warehouse = Warehouse.Create(tenantId, branch.Id, "Almacén Principal", isDefault: true);
        dbContext.Warehouses.Add(warehouse);

        var register = CashRegister.Create(tenantId, branch.Id, $"Caja Express_{Guid.NewGuid()}", $"CR-{Guid.NewGuid().ToString()[..6]}");
        dbContext.CashRegisters.Add(register);

        var category = Category.Create($"Snacks_{Guid.NewGuid()}", "Categoría snacks");
        dbContext.Categories.Add(category);

        var product = Product.Create(
            $"Papas Fritas 100g_{Guid.NewGuid()}",
            Sku.Create($"SKU-{Guid.NewGuid().ToString()[..8]}"),
            Money.Create(2.50m, "USD"),
            category.Id
        );
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        await dispatcher.SendAsync(new RecordInventoryMovementCommand(
            product.Id,
            warehouse.Id,
            3m,
            InventoryMovementType.Adjustment,
            Notes: "Stock inicial 3 unidades"
        ));

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

        // Act
        var result = await dispatcher.SendAsync(createSaleCommand);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("StockLevel.InsufficientStock", result.Error.Code);

        var saleInDb = await dbContext.Sales.AsNoTracking().FirstOrDefaultAsync(s => s.ReceiptNumber == receiptNumber);
        Assert.Null(saleInDb);

        var stockLevel = await stockLevelRepo.GetAsync(product.Id, warehouse.Id, null);
        Assert.NotNull(stockLevel);
        Assert.Equal(3m, stockLevel.QuantityAvailable); // Stock intacto de 3 unidades
    }

    [Fact]
    public async Task Scenario3_SaleWithoutOpenSession_ShouldReturnConflictResult_WithoutException()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        var serviceProvider = _fixture.CreateServiceProvider(tenantId);
        using var scope = serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var dbContext = sp.GetRequiredService<PosDbContext>();

        var address = Address.Create("Av. Marina 200", "Lima", "15088", "PE");
        var branch = Branch.Create(tenantId, $"Sucursal Oeste_{Guid.NewGuid()}", address, "+51122222222");
        dbContext.Branches.Add(branch);

        var warehouse = Warehouse.Create(tenantId, branch.Id, "Almacén Principal", isDefault: true);
        dbContext.Warehouses.Add(warehouse);

        var register = CashRegister.Create(tenantId, branch.Id, $"Caja 03_{Guid.NewGuid()}", $"CR-{Guid.NewGuid().ToString()[..6]}");
        dbContext.CashRegisters.Add(register);

        var category = Category.Create($"Limpieza_{Guid.NewGuid()}", "Categoría limpieza");
        dbContext.Categories.Add(category);

        var product = Product.Create(
            $"Detergente 1L_{Guid.NewGuid()}",
            Sku.Create($"SKU-{Guid.NewGuid().ToString()[..8]}"),
            Money.Create(5.00m, "USD"),
            category.Id
        );
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        await dispatcher.SendAsync(new RecordInventoryMovementCommand(
            product.Id,
            warehouse.Id,
            10m,
            InventoryMovementType.Adjustment
        ));

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

        // Assert - Falla con conflicto de negocio controlado
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

        // Crear producto, almacén y stock para Tenant A
        using var scopeA = spTenantA.CreateScope();
        var dispatcherA = scopeA.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbA = scopeA.ServiceProvider.GetRequiredService<PosDbContext>();
        var stockRepoA = scopeA.ServiceProvider.GetRequiredService<IStockLevelRepository>();

        var addrA = Address.Create("Av. A 123", "Lima", "15001", "PE");
        var branchA = Branch.Create(tenantA, $"BranchA_{Guid.NewGuid()}", addrA, "+511111111");
        dbA.Branches.Add(branchA);
        var whA = Warehouse.Create(tenantA, branchA.Id, "Almacén A", isDefault: true);
        dbA.Warehouses.Add(whA);
        var regA = CashRegister.Create(tenantA, branchA.Id, "Caja A", $"CRA-{Guid.NewGuid().ToString()[..4]}");
        dbA.CashRegisters.Add(regA);
        var catA = Category.Create($"CatA_{Guid.NewGuid()}", "Cat Tenant A");
        dbA.Categories.Add(catA);
        var prodA = Product.Create($"ProdA_{Guid.NewGuid()}", Sku.Create($"SKUA-{Guid.NewGuid().ToString()[..6]}"), Money.Create(10m, "USD"), catA.Id);
        dbA.Products.Add(prodA);
        await dbA.SaveChangesAsync();
        await dispatcherA.SendAsync(new RecordInventoryMovementCommand(prodA.Id, whA.Id, 10m, InventoryMovementType.Adjustment));

        // Crear producto, almacén y stock para Tenant B
        using var scopeB = spTenantB.CreateScope();
        var dispatcherB = scopeB.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<PosDbContext>();
        var stockRepoB = scopeB.ServiceProvider.GetRequiredService<IStockLevelRepository>();

        var addrB = Address.Create("Av. B 456", "Lima", "15002", "PE");
        var branchB = Branch.Create(tenantB, $"BranchB_{Guid.NewGuid()}", addrB, "+512222222");
        dbB.Branches.Add(branchB);
        var whB = Warehouse.Create(tenantB, branchB.Id, "Almacén B", isDefault: true);
        dbB.Warehouses.Add(whB);
        var catB = Category.Create($"CatB_{Guid.NewGuid()}", "Cat Tenant B");
        dbB.Categories.Add(catB);
        var prodB = Product.Create($"ProdB_{Guid.NewGuid()}", Sku.Create($"SKUB-{Guid.NewGuid().ToString()[..6]}"), Money.Create(20m, "USD"), catB.Id);
        dbB.Products.Add(prodB);
        await dbB.SaveChangesAsync();
        await dispatcherB.SendAsync(new RecordInventoryMovementCommand(prodB.Id, whB.Id, 20m, InventoryMovementType.Adjustment));

        // Abrir Caja y vender 2 unidades en Tenant A
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

        // Assert - Aislamiento Multi-Tenant sobre StockLevel
        // Tenant A stock disponible bajó a 8m
        var stockA = await stockRepoA.GetAsync(prodA.Id, whA.Id, null);
        Assert.NotNull(stockA);
        Assert.Equal(8m, stockA.QuantityAvailable);

        // Tenant B stock disponible se mantiene intacto en 20m y no fue afectado por la venta de Tenant A
        var stockB = await stockRepoB.GetAsync(prodB.Id, whB.Id, null);
        Assert.NotNull(stockB);
        Assert.Equal(20m, stockB.QuantityAvailable);
    }
}
