using Pos.Application.Common.Interfaces;
using Pos.Application.Suppliers.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Suppliers;

public class SuppliersCommandHandlerTests
{
    private readonly FakeSupplierRepository _supplierRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    [Fact]
    public async Task CreateSupplier_WithValidData_ShouldCreateSupplierAndReturnSuccess()
    {
        // Arrange
        var command = new CreateSupplierCommand(
            "Distribuidora Peru",
            "20111222333",
            "PE",
            "Av. Industrial 500",
            "Lima",
            "15003",
            "PE",
            "Juan Perez",
            "contacto@distperu.com",
            "+51988776655"
        );
        var handler = new CreateSupplierCommandHandler(_supplierRepository, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Distribuidora Peru", result.Value.Name);
        Assert.Single(_supplierRepository.Suppliers);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task CreateSupplier_WhenTaxIdAlreadyExists_ShouldReturnConflictResult()
    {
        // Arrange
        var existingAddress = Address.Create("Calle A 100", "Lima", "15001", "PE");
        var existingSupplier = Supplier.Create(
            "Proveedor A",
            TaxId.Create("20111222333", "PE"),
            existingAddress,
            "Contacto A",
            "a@supplier.com",
            "+51900000001"
        );
        _supplierRepository.Suppliers.Add(existingSupplier);

        var command = new CreateSupplierCommand(
            "Proveedor B",
            "20111222333",
            "PE",
            "Calle B 200",
            "Lima",
            "15002",
            "PE",
            "Contacto B",
            "b@supplier.com",
            "+51900000002"
        );
        var handler = new CreateSupplierCommandHandler(_supplierRepository, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Supplier.AlreadyExists", result.Error.Code);
        Assert.Single(_supplierRepository.Suppliers);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task UpdateSupplier_WhenSupplierDoesNotExist_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new UpdateSupplierCommand(
            Guid.NewGuid(),
            "Proveedor Actualizado",
            "20111222333",
            "PE",
            "Calle N",
            "Lima",
            "15001",
            "PE",
            "Contacto",
            "test@supplier.com",
            "+51900000000"
        );
        var handler = new UpdateSupplierCommandHandler(_supplierRepository, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Supplier.NotFound", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
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
