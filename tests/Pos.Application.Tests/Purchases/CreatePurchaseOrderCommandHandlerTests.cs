using Pos.Application.Tests.Support.Fakes;
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

}
