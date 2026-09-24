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
    private sealed class FakeInventoryRepository : IInventoryRepository
    {
        public List<InventoryMovement> Movements { get; } = [];
        public Dictionary<Guid, decimal> StockByProduct { get; } = [];

        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
        {
            Movements.Add(movement);
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<InventoryMovement> Items, int TotalCount)> GetMovementsPagedAsync(
            Guid? productId, Guid? warehouseId, InventoryMovementType? movementType, DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var items = productId.HasValue ? Movements.Where(m => m.ProductId == productId.Value).ToList() : Movements;
            return Task.FromResult<(IReadOnlyList<InventoryMovement>, int)>((items, items.Count));
        }

        public Task<decimal> ReconcileStockFromLedgerAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(StockByProduct.TryGetValue(productId, out decimal stock) ? stock : 0m);
        }
    }

    private sealed class FakeStockLevelRepository : IStockLevelRepository
    {
        public List<StockLevel> StockLevels { get; } = [];
        public Task AddAsync(StockLevel stockLevel, CancellationToken cancellationToken = default) { StockLevels.Add(stockLevel); return Task.CompletedTask; }
        public Task<StockLevel?> GetAsync(Guid productId, Guid warehouseId, Guid? containerId, CancellationToken cancellationToken = default) => Task.FromResult(StockLevels.FirstOrDefault(s => s.ProductId == productId && s.WarehouseId == warehouseId && s.ContainerId == containerId));
        public Task<IReadOnlyList<StockLevel>> GetByWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StockLevel>>(StockLevels.Where(s => s.WarehouseId == warehouseId).ToList());
        public Task<IReadOnlyList<StockLevel>> GetBelowThresholdAsync(Guid? warehouseId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StockLevel>>(StockLevels.Where(s => s.QuantityAvailable <= s.MinStockThreshold).ToList());
        public void Update(StockLevel stockLevel) { }
    }

    private sealed class FakeWarehouseRepository : IWarehouseRepository
    {
        public List<Warehouse> Warehouses { get; } = [];
        public Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default) { Warehouses.Add(warehouse); return Task.CompletedTask; }
        public Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Warehouses.FirstOrDefault(w => w.Id == id));
        public Task<Warehouse?> GetDefaultAsync(CancellationToken cancellationToken = default) => Task.FromResult(Warehouses.FirstOrDefault(w => w.IsDefault));
        public Task<IReadOnlyList<Warehouse>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Warehouse>>(Warehouses);
        public Task<int> CountByTenantAsync(CancellationToken cancellationToken = default) => Task.FromResult(Warehouses.Count);
        public void Update(Warehouse warehouse) { }
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> Products { get; } = [];
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Products.FirstOrDefault(p => p.Id == id));
        public Task<Product?> GetBySkuAsync(Sku sku, CancellationToken cancellationToken = default) => Task.FromResult(Products.FirstOrDefault(p => p.Sku.Value == sku.Value));
        public Task<bool> ExistsBySkuAsync(Sku sku, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(Products.Any(p => p.Sku.Value == sku.Value && (excludeId == null || p.Id != excludeId.Value)));
        public Task AddAsync(Product product, CancellationToken cancellationToken = default) { Products.Add(product); return Task.CompletedTask; }
        public void Update(Product product) { }
        public void Delete(Product product) { Products.Remove(product); }
        public Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm, Guid? categoryId, bool? isActive = null, CancellationToken cancellationToken = default) => Task.FromResult< (IReadOnlyList<Product>, int) >((Products, Products.Count));
    }

    private sealed class FakeTenantContext : ICurrentTenantContext
    {
        public Guid? TenantId { get; set; }
        public bool IsSuperAdmin => false;
        public bool HasTenant => TenantId.HasValue;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveChangesCount++; return Task.FromResult(1); }
    }
}
