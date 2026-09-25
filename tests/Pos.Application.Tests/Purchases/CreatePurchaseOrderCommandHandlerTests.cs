using Pos.Application.Common.Interfaces;
using Pos.Application.PurchaseOrders.Commands;
using Pos.Application.PurchaseOrders.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Pos.Domain.Enums;
using Xunit;

namespace Pos.Application.Tests.Purchases;

public class CreatePurchaseOrderCommandHandlerTests
{
    private readonly FakePurchaseOrderRepository _orderRepository = new();
    private readonly FakeSupplierRepository _supplierRepository = new();
    private readonly FakeWarehouseRepository _warehouseRepository = new();
    private readonly FakeProductRepository _productRepository = new();
    private readonly FakeTenantContext _tenantContext = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CreatePurchaseOrderCommandHandler _handler;

    public CreatePurchaseOrderCommandHandlerTests()
    {
        _handler = new CreatePurchaseOrderCommandHandler(
            _orderRepository,
            _supplierRepository,
            _warehouseRepository,
            _productRepository,
            _tenantContext,
            _unitOfWork);
    }

    [Fact]
    public async Task HandleAsync_WithInactiveSupplier_ShouldReturnConflictResult()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        _tenantContext.TenantId = tenantId;

        var supplier = Supplier.Create(
            "Proveedor",
            TaxId.Create("12345678"),
            Address.Create("Calle 1", "Ciudad", "0000", "País"),
            "Contacto",
            "prov@test.com",
            "123456789");
        supplier.Deactivate(); // Set to inactive
        _supplierRepository.Suppliers.Add(supplier);

        var warehouse = Warehouse.Create(tenantId, Guid.NewGuid(), "Almacén Principal", isDefault: true);
        _warehouseRepository.Warehouses.Add(warehouse);

        var product = Product.Create("Producto A", Sku.Create("PROD-A-001"), Money.Create(50m, "USD"), Guid.NewGuid());
        _productRepository.Products.Add(product);

        var command = new CreatePurchaseOrderCommand(
            supplier.Id,
            warehouse.Id,
            "PO-001",
            new List<CreatePurchaseOrderLineDto>
            {
                new(product.Id, 10m, 40m)
            });

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Supplier.Inactive", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_WithInactiveProduct_ShouldReturnConflictResult()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        _tenantContext.TenantId = tenantId;

        var supplier = Supplier.Create(
            "Proveedor",
            TaxId.Create("12345678"),
            Address.Create("Calle 1", "Ciudad", "0000", "País"),
            "Contacto",
            "prov@test.com",
            "123456789");
        _supplierRepository.Suppliers.Add(supplier);

        var warehouse = Warehouse.Create(tenantId, Guid.NewGuid(), "Almacén Principal", isDefault: true);
        _warehouseRepository.Warehouses.Add(warehouse);

        var product = Product.Create("Producto Inactivo", Sku.Create("PROD-A-002"), Money.Create(50m, "USD"), Guid.NewGuid());
        product.Deactivate(); // Set to inactive
        _productRepository.Products.Add(product);

        var command = new CreatePurchaseOrderCommand(
            supplier.Id,
            warehouse.Id,
            "PO-002",
            new List<CreatePurchaseOrderLineDto>
            {
                new(product.Id, 10m, 40m)
            });

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Product.Inactive", result.Error.Code);
    }

    // Fakes
    private sealed class FakePurchaseOrderRepository : IPurchaseOrderRepository
    {
        public List<PurchaseOrder> Orders { get; } = [];
        public Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Orders.FirstOrDefault(o => o.Id == id));
        public Task<PurchaseOrder?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default) => Task.FromResult(Orders.FirstOrDefault(o => o.OrderNumber == orderNumber));
        public Task AddAsync(PurchaseOrder order, CancellationToken cancellationToken = default) { Orders.Add(order); return Task.CompletedTask; }
        public void Update(PurchaseOrder order) { }
        public Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, PurchaseOrderStatus? status = null, Guid? supplierId = null, Guid? warehouseId = null, CancellationToken cancellationToken = default)
        {
            var filtered = Orders
                .Where(o => (!status.HasValue || o.Status == status.Value)
                         && (!supplierId.HasValue || o.SupplierId == supplierId.Value)
                         && (!warehouseId.HasValue || o.WarehouseId == warehouseId.Value))
                .ToList();
            var items = filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult<(IReadOnlyList<PurchaseOrder>, int)>((items, filtered.Count));
        }
    }

    private sealed class FakeSupplierRepository : ISupplierRepository
    {
        public List<Supplier> Suppliers { get; } = [];
        public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Suppliers.FirstOrDefault(s => s.Id == id));
        public Task<Supplier?> GetByTaxIdAsync(TaxId taxId, CancellationToken cancellationToken = default) => Task.FromResult<Supplier?>(null);
        public Task<bool> ExistsByTaxIdAsync(TaxId taxId, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default) { Suppliers.Add(supplier); return Task.CompletedTask; }
        public void Update(Supplier supplier) { }
        public void Delete(Supplier supplier) { }
        public Task<(IReadOnlyList<Supplier> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm, bool? isActive = null, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<Supplier>, int)>((Suppliers, Suppliers.Count));
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
        public Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm, Guid? categoryId, bool? isActive = null, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<Product>, int)>((Products, Products.Count));
        public Task<bool> ExistsBySkuAsync(Sku sku, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(Product product, CancellationToken cancellationToken = default) { Products.Add(product); return Task.CompletedTask; }
        public void Update(Product product) { }
        public void Delete(Product product) { }
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
