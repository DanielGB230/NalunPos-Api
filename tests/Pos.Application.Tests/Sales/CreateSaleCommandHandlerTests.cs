using Pos.Application.Tests.Support.Fakes;
using Pos.Application.Common.Interfaces;
using Pos.Application.Sales.Commands;
using Pos.Application.Sales.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Sales;

public class CreateSaleCommandHandlerTests
{
    private readonly FakeSaleRepository _saleRepository = new();
    private readonly FakeCashRegisterRepository _registerRepository = new();
    private readonly FakeCustomerRepository _customerRepository = new();
    private readonly FakeProductRepository _productRepository = new();
    private readonly FakeInventoryRepository _inventoryRepository = new();
    private readonly FakeWarehouseRepository _warehouseRepository = new();
    private readonly FakeStockLevelRepository _stockLevelRepository = new();
    private readonly FakeTenantContext _tenantContext = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeDispatcher _dispatcher = new();
    private readonly CreateSaleCommandHandler _handler;

    public CreateSaleCommandHandlerTests()
    {
        _handler = new CreateSaleCommandHandler(
            _saleRepository,
            _registerRepository,
            _customerRepository,
            _productRepository,
            _inventoryRepository,
            _warehouseRepository,
            _stockLevelRepository,
            _tenantContext,
            _unitOfWork,
            _dispatcher);
    }

    [Fact]
    public async Task HandleAsync_WithOpenSessionAndValidLineItems_ShouldCreateSaleAndReturnSuccessResult()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        _tenantContext.TenantId = tenantId;

        var warehouse = Warehouse.Create(tenantId, Guid.NewGuid(), "Almacén Principal", isDefault: true);
        _warehouseRepository.Warehouses.Add(warehouse);

        var session = CashRegisterSession.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Money.Create(100m, "USD"),
            "Apertura inicial");
        _registerRepository.Sessions.Add(session);

        var product = Product.Create(
            "Producto A",
            Sku.Create("PROD-A-001"),
            Money.Create(50m, "USD"),
            Guid.NewGuid());

        _productRepository.Products.Add(product);

        var stockLevel = StockLevel.Create(tenantId, product.Id, warehouse.Id);
        stockLevel.Increment(10m);
        _stockLevelRepository.StockLevels.Add(stockLevel);

        var command = new CreateSaleCommand(
            "V-001-0001",
            session.Id,
            CustomerId: null,
            LineItems: new List<CreateSaleItemDto>
            {
                new(product.Id, product.Name, 2, 50m)
            },
            TaxRatePercentage: 18m,
            Currency: "USD");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("V-001-0001", result.Value.ReceiptNumber);
        Assert.Equal(118m, result.Value.TotalAmount); // 100 + 18% tax
        Assert.Single(_saleRepository.Sales);
        Assert.Equal(8m, stockLevel.QuantityAvailable);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WithClosedCashRegisterSession_ShouldReturnConflictResult()
    {
        // Arrange
        var session = CashRegisterSession.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Money.Create(100m, "USD"),
            "Apertura previa");
        session.Close(Money.Create(100m, "USD"), Money.Create(100m, "USD"), "Cierre de sesión");
        _registerRepository.Sessions.Add(session);

        var command = new CreateSaleCommand(
            "V-001-0002",
            session.Id,
            CustomerId: null,
            LineItems: new List<CreateSaleItemDto>
            {
                new(Guid.NewGuid(), "Producto B", 1, 20m)
            });

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("CashRegisterSession.Closed", result.Error.Code);
        Assert.Empty(_saleRepository.Sales);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WithInactiveCustomer_ShouldReturnConflictResult()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        _tenantContext.TenantId = tenantId;

        var warehouse = Warehouse.Create(tenantId, Guid.NewGuid(), "Almacén Principal", isDefault: true);
        _warehouseRepository.Warehouses.Add(warehouse);

        var session = CashRegisterSession.Open(Guid.NewGuid(), Guid.NewGuid(), Money.Create(100m, "USD"), "Apertura");
        _registerRepository.Sessions.Add(session);

        var customer = Customer.Create(
            "Cliente Inactivo",
            TaxId.Create("12345678-9", "PE"),
            "inactivo@test.com");
        customer.Deactivate(); // Set to inactive
        _customerRepository.Customers.Add(customer);

        var command = new CreateSaleCommand(
            "V-001-0003",
            session.Id,
            CustomerId: customer.Id,
            LineItems: new List<CreateSaleItemDto>
            {
                new(Guid.NewGuid(), "Producto B", 1, 20m)
            });

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Customer.Inactive", result.Error.Code);
    }

    // Fakes de prueba

    private sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> Products { get; } = [];
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Products.FirstOrDefault(p => p.Id == id));
        public Task<Product?> GetBySkuAsync(Sku sku, CancellationToken cancellationToken = default) => Task.FromResult(Products.FirstOrDefault(p => p.Sku.Value == sku.Value));
        public Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm, Guid? categoryId, bool? isActive = null, CancellationToken cancellationToken = default) => Task.FromResult< (IReadOnlyList<Product>, int) >((Products, Products.Count));
        public Task<bool> ExistsBySkuAsync(Sku sku, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(Product product, CancellationToken cancellationToken = default) { Products.Add(product); return Task.CompletedTask; }
        public void Update(Product product) { }
        public void Delete(Product product) { }
    }

    private sealed class FakeInventoryRepository : IInventoryRepository
    {
        public Dictionary<Guid, decimal> Stocks { get; } = new();
        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<(IReadOnlyList<InventoryMovement> Items, int TotalCount)> GetMovementsPagedAsync(
            Guid? productId, Guid? warehouseId, InventoryMovementType? movementType, DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<InventoryMovement>, int)>(([], 0));
        public Task<decimal> ReconcileStockFromLedgerAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default) => Task.FromResult(0m);
    }

}
