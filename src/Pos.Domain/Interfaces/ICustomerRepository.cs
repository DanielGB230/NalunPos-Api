using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Customer?> GetByTaxIdAsync(TaxId taxId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Customer> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm,
        bool? isActive = null,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByTaxIdAsync(TaxId taxId, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);
    void Update(Customer customer);
    void Delete(Customer customer);
}
