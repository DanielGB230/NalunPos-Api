using Pos.Domain.Entities;

namespace Pos.Domain.Interfaces;

public interface ISaleRepository
{
    Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Sale?> GetByReceiptNumberAsync(string receiptNumber, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Sale> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? sessionId,
        Guid? customerId,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default);
    Task AddAsync(Sale sale, CancellationToken cancellationToken = default);
    void Update(Sale sale);
}
