using Pos.Domain.Entities;

namespace Pos.Domain.Interfaces;

public interface IInventoryRepository
{
    Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default);
    Task<decimal> GetCurrentStockAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<InventoryMovement> Items, int TotalCount)> GetMovementsHistoryPagedAsync(
        Guid productId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
