using Pos.Domain.Entities;

namespace Pos.Domain.Interfaces;

public interface IPurchaseRepository
{
    Task<Purchase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Purchase?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Purchase> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? supplierId,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default);
    Task AddAsync(Purchase purchase, CancellationToken cancellationToken = default);
    void Update(Purchase purchase);
}
