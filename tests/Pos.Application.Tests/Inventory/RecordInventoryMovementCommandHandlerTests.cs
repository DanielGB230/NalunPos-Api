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
    private readonly FakeProductRepository _productRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordInventoryMovementCommandHandler _handler;

    public RecordInventoryMovementCommandHandlerTests()
    {
        _handler = new RecordInventoryMovementCommandHandler(
            _inventoryRepository,
            _productRepository,
            _unitOfWork);
    }

    [Fact]
    public async Task HandleAsync_WithValidProductAndSufficientStock_ShouldRecordMovementAndReturnSuccess()
    {
        // Arrange
        var product = Product.Create("Laptop Gamer", Sku.Create("LAP-001"), Money.Create(1200m, "USD"), Guid.NewGuid(), initialStock: 10);
        _productRepository.Products.Add(product);
        _inventoryRepository.StockByProduct[product.Id] = 10m;

        var command = new RecordInventoryMovementCommand(
            product.Id,
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
        Assert.Equal(15, product.StockQuantity);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WithExceedingStockOutput_ShouldReturnValidationErrorResult()
    {
        // Arrange
        var product = Product.Create("Teclado Mecánico", Sku.Create("TEC-002"), Money.Create(80m, "USD"), Guid.NewGuid(), initialStock: 2);
        _productRepository.Products.Add(product);
        _inventoryRepository.StockByProduct[product.Id] = 2m;

        var command = new RecordInventoryMovementCommand(
            product.Id,
            Quantity: -10m,
            MovementType: InventoryMovementType.Adjustment,
            Notes: "Intento de salida excesiva");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Inventory.InsufficientStock", result.Error.Code);
        Assert.Empty(_inventoryRepository.Movements);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    // Fakes de prueba
    private sealed class FakeInventoryRepository : IInventoryRepository
    {
        public List<InventoryMovement> Movements { get; } = [];
        public Dictionary<Guid, decimal> StockByProduct { get; } = [];

        public Task<decimal> GetCurrentStockAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(StockByProduct.TryGetValue(productId, out decimal stock) ? stock : 0m);
        }

        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
        {
            Movements.Add(movement);
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<InventoryMovement> Items, int TotalCount)> GetMovementsHistoryPagedAsync(Guid productId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var items = Movements.Where(m => m.ProductId == productId).ToList();
            return Task.FromResult< (IReadOnlyList<InventoryMovement>, int) >((items, items.Count));
        }

        public Task<(IReadOnlyList<InventoryMovement> Items, int TotalCount)> GetMovementHistoryAsync(Guid productId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var items = Movements.Where(m => m.ProductId == productId).ToList();
            return Task.FromResult< (IReadOnlyList<InventoryMovement>, int) >((items, items.Count));
        }
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
        public Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm, Guid? categoryId, bool? isActiveOnly, CancellationToken cancellationToken = default) => Task.FromResult< (IReadOnlyList<Product>, int) >((Products, Products.Count));
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveChangesCount++; return Task.FromResult(1); }
    }
}
