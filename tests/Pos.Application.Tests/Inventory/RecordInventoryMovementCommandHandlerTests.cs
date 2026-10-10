using Pos.Application.Tests.Support.Fakes;
using Pos.Application.Common.Interfaces;
using Pos.Application.Inventory.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Inventory;

public class RecordInventoryMovementCommandHandlerTests
{
    private readonly FakeInventoryRepository _inventoryRepository = new();
    private readonly FakeStockLevelRepository _stockLevelRepository = new();
    private readonly FakeWarehouseRepository _warehouseRepository = new();
    private readonly FakeProductRepository _productRepository = new();
    private readonly FakeTenantContext _tenantContext = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordInventoryMovementCommandHandler _handler;

    public RecordInventoryMovementCommandHandlerTests()
    {
        _handler = new RecordInventoryMovementCommandHandler(
            _inventoryRepository,
            _stockLevelRepository,
            _warehouseRepository,
            _productRepository,
            _tenantContext,
            _unitOfWork);
    }

    [Fact]
    public async Task HandleAsync_WithValidProductAndSufficientStock_ShouldRecordMovementAndReturnSuccess()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        _tenantContext.TenantId = tenantId;

        var warehouse = Warehouse.Create(tenantId, Guid.NewGuid(), "Almacén Principal", isDefault: true);
        _warehouseRepository.Warehouses.Add(warehouse);

        var product = Product.Create("Laptop Gamer", Sku.Create("LAP-001"), Money.Create(1200m, "USD"), Guid.NewGuid());
        _productRepository.Products.Add(product);

        var stockLevel = StockLevel.Create(tenantId, product.Id, warehouse.Id);
        stockLevel.Increment(10m);
        _stockLevelRepository.StockLevels.Add(stockLevel);

        var command = new RecordInventoryMovementCommand(
            product.Id,
            warehouse.Id,
            Quantity: 5m,
            MovementType: InventoryMovementType.Purchase,
            Notes: "Ingreso de mercadería");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(5m, result.Value.Quantity);
        Assert.Single(_inventoryRepository.Movements);
        Assert.Equal(15m, stockLevel.QuantityAvailable);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WithExceedingStockOutput_ShouldReturnValidationErrorResult()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        _tenantContext.TenantId = tenantId;

        var warehouse = Warehouse.Create(tenantId, Guid.NewGuid(), "Almacén Principal", isDefault: true);
        _warehouseRepository.Warehouses.Add(warehouse);

        var product = Product.Create("Teclado Mecánico", Sku.Create("TEC-002"), Money.Create(80m, "USD"), Guid.NewGuid());
        _productRepository.Products.Add(product);

        var stockLevel = StockLevel.Create(tenantId, product.Id, warehouse.Id);
        stockLevel.Increment(2m);
        _stockLevelRepository.StockLevels.Add(stockLevel);

        var command = new RecordInventoryMovementCommand(
            product.Id,
            warehouse.Id,
            Quantity: -10m,
            MovementType: InventoryMovementType.Adjustment,
            Notes: "Intento de salida excesiva");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("StockLevel.InsufficientStock", result.Error.Code);
        Assert.Empty(_inventoryRepository.Movements);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    // Fakes de prueba

}
