using Pos.Domain.Entities;

namespace Pos.Domain.Interfaces;

public interface IStockAdjustmentRepository
{
    Task AddAsync(StockAdjustment adjustment, CancellationToken cancellationToken = default);
    Task<StockAdjustment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<StockAdjustment> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
