using Pos.Domain.Entities;

namespace Pos.Domain.Interfaces;

public interface IStockTransferRepository
{
    Task AddAsync(StockTransfer transfer, CancellationToken cancellationToken = default);
    Task<StockTransfer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<StockTransfer> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
