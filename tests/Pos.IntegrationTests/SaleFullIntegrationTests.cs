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
}
