using Pos.Application.Common.Interfaces;
using Pos.Application.Purchases.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Purchases;

public class CreatePurchaseCommandHandlerTests
{
    private readonly FakePurchaseRepository _purchaseRepository = new();
    private readonly FakeSupplierRepository _supplierRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CreatePurchaseCommandHandler _handler;

    public CreatePurchaseCommandHandlerTests()
    {
        _handler = new CreatePurchaseCommandHandler(_purchaseRepository, _supplierRepository, _unitOfWork);
    }

    [Fact]
    public async Task HandleAsync_WithValidSupplierAndData_ShouldCreatePurchaseAndReturnSuccess()
    {
        // Arrange
        var address = Address.Create("Calle Principal 123", "Lima", "15001", "PE");
        var supplier = Supplier.Create("Proveedor Test", TaxId.Create("12345678901", "PE"), address, "Contacto", "test@supplier.com", "+1234567890");
        _supplierRepository.Suppliers.Add(supplier);

        var command = new CreatePurchaseCommand(
            supplier.Id,
            "PO-2026-001",
            new List<CreatePurchaseItemDto>
            {
                new(Guid.NewGuid(), "Insumo A", 10, 25m)
            },
            "USD"
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("PO-2026-001", result.Value.OrderNumber);
        Assert.Equal(250m, result.Value.TotalAmount);
        Assert.Single(_purchaseRepository.Purchases);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenSupplierDoesNotExist_ShouldReturnNotFoundResult()
    {
        // Arrange
        var nonExistentSupplierId = Guid.NewGuid();
        var command = new CreatePurchaseCommand(
            nonExistentSupplierId,
            "PO-2026-002",
            new List<CreatePurchaseItemDto>
            {
                new(Guid.NewGuid(), "Insumo B", 5, 10m)
            }
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Supplier.NotFound", result.Error.Code);
        Assert.Empty(_purchaseRepository.Purchases);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    private sealed class FakePurchaseRepository : IPurchaseRepository
    {
        public List<Purchase> Purchases { get; } = [];

        public Task<Purchase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Purchases.FirstOrDefault(p => p.Id == id));
        public Task<Purchase?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default) => Task.FromResult(Purchases.FirstOrDefault(p => p.OrderNumber == orderNumber));
        public Task AddAsync(Purchase purchase, CancellationToken cancellationToken = default) { Purchases.Add(purchase); return Task.CompletedTask; }
        public void Update(Purchase purchase) { }
        public Task<(IReadOnlyList<Purchase> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Guid? supplierId, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<Purchase>, int)>((Purchases, Purchases.Count));
    }

    private sealed class FakeSupplierRepository : ISupplierRepository
    {
        public List<Supplier> Suppliers { get; } = [];

        public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Suppliers.FirstOrDefault(s => s.Id == id));
        public Task<Supplier?> GetByTaxIdAsync(TaxId taxId, CancellationToken cancellationToken = default) => Task.FromResult(Suppliers.FirstOrDefault(s => s.TaxId.Value == taxId.Value));
        public Task<bool> ExistsByTaxIdAsync(TaxId taxId, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(Suppliers.Any(s => s.TaxId.Value == taxId.Value && s.Id != excludeId));
        public Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default) { Suppliers.Add(supplier); return Task.CompletedTask; }
        public void Update(Supplier supplier) { }
        public void Delete(Supplier supplier) => Suppliers.Remove(supplier);
        public Task<(IReadOnlyList<Supplier> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm, bool? isActiveOnly, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<Supplier>, int)>((Suppliers, Suppliers.Count));
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveChangesCount++; return Task.FromResult(1); }
    }
}
