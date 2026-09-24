using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Interfaces;

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Supplier?> GetByTaxIdAsync(TaxId taxId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Supplier> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm,
        bool? isActive = null,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByTaxIdAsync(TaxId taxId, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default);
    void Update(Supplier supplier);
    void Delete(Supplier supplier);
}
