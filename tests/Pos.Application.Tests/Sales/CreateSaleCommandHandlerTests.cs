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
    private sealed class FakeSaleRepository : ISaleRepository
    {
        public List<Sale> Sales { get; } = [];
        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Sales.FirstOrDefault(s => s.Id == id));
        public Task<Sale?> GetByReceiptNumberAsync(string receiptNumber, CancellationToken cancellationToken = default) => Task.FromResult(Sales.FirstOrDefault(s => s.ReceiptNumber.Equals(receiptNumber, StringComparison.OrdinalIgnoreCase)));
        public Task AddAsync(Sale sale, CancellationToken cancellationToken = default) { Sales.Add(sale); return Task.CompletedTask; }
        public void Update(Sale sale) { }
        public Task<(IReadOnlyList<Sale> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Guid? sessionId, Guid? customerId, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken = default) => Task.FromResult< (IReadOnlyList<Sale>, int) >((Sales.AsReadOnly(), Sales.Count));
    }

    private sealed class FakeCashRegisterRepository : ICashRegisterRepository
    {
        public List<CashRegisterSession> Sessions { get; } = [];
        public Task<CashRegister?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<CashRegister?>(null);
        public Task<IReadOnlyList<CashRegister>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CashRegister>>([]);
        public Task AddAsync(CashRegister register, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(CashRegister register) { }
        public Task<CashRegisterSession?> GetSessionByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult(Sessions.FirstOrDefault(s => s.Id == sessionId));
        public Task<CashRegisterSession?> GetActiveSessionByRegisterIdAsync(Guid registerId, CancellationToken cancellationToken = default) => Task.FromResult(Sessions.FirstOrDefault(s => s.CashRegisterId == registerId && s.Status == SessionStatus.Open));
        public Task AddSessionAsync(CashRegisterSession session, CancellationToken cancellationToken = default) { Sessions.Add(session); return Task.CompletedTask; }
        public void UpdateSession(CashRegisterSession session) { }
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        public List<Customer> Customers { get; } = [];
        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Customers.FirstOrDefault(c => c.Id == id));
        public Task<Customer?> GetByTaxIdAsync(TaxId taxId, CancellationToken cancellationToken = default) => Task.FromResult<Customer?>(null);
        public Task<bool> ExistsByTaxIdAsync(TaxId taxId, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(Customer customer, CancellationToken cancellationToken = default) { Customers.Add(customer); return Task.CompletedTask; }
        public void Update(Customer customer) { }
        public void Delete(Customer customer) { }
        public Task<(IReadOnlyList<Customer> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm, bool? isActive = null, CancellationToken cancellationToken = default) => Task.FromResult< (IReadOnlyList<Customer>, int) >((Customers, Customers.Count));
    }

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

    private sealed class FakeStockLevelRepository : IStockLevelRepository
    {
        public List<StockLevel> StockLevels { get; } = [];
        public Task AddAsync(StockLevel stockLevel, CancellationToken cancellationToken = default) { StockLevels.Add(stockLevel); return Task.CompletedTask; }
        public Task<StockLevel?> GetAsync(Guid productId, Guid warehouseId, Guid? containerId, CancellationToken cancellationToken = default) => Task.FromResult(StockLevels.FirstOrDefault(s => s.ProductId == productId && s.WarehouseId == warehouseId && s.ContainerId == containerId));
        public Task<IReadOnlyList<StockLevel>> GetByWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StockLevel>>(StockLevels.Where(s => s.WarehouseId == warehouseId).ToList());
        public Task<IReadOnlyList<StockLevel>> GetBelowThresholdAsync(Guid? warehouseId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StockLevel>>(StockLevels.Where(s => s.QuantityAvailable <= s.MinStockThreshold).ToList());
        public void Update(StockLevel stockLevel) { }
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

    private sealed class FakeDispatcher : IDispatcher
    {
        public Task SendAsync(ICommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task PublishAsync<TDomainEvent>(TDomainEvent domainEvent, CancellationToken cancellationToken = default) where TDomainEvent : IDomainEvent => Task.CompletedTask;
        public Task PublishIntegrationEventAsync<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken cancellationToken = default) where TIntegrationEvent : Pos.Application.IntegrationEvents.Contracts.IIntegrationEvent => Task.CompletedTask;
    }
}
