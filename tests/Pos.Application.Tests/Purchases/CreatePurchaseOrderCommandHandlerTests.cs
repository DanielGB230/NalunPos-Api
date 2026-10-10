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

}
