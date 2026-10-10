namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;

public sealed class FakeInventoryRepository : IInventoryRepository
{
    public List<InventoryMovement> Movements { get; } = [];
    public Dictionary<Guid, decimal> Stocks { get; } = [];
    public Dictionary<Guid, decimal> StockByProduct { get; } = [];
    public int CallCount { get; private set; }

    public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
    {
        CallCount++;
        Movements.Add(movement);
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<InventoryMovement> Items, int TotalCount)> GetMovementsPagedAsync(
        Guid? productId,
        Guid? warehouseId,
        InventoryMovementType? movementType,
        DateTime? dateFrom,
        DateTime? dateTo,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        var items = productId.HasValue ? Movements.Where(m => m.ProductId == productId.Value).ToList() : Movements;
        return Task.FromResult<(IReadOnlyList<InventoryMovement>, int)>((items, items.Count));
    }

    public Task<decimal> ReconcileStockFromLedgerAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default)
    {
        CallCount++;
        if (StockByProduct.TryGetValue(productId, out decimal s1)) return Task.FromResult(s1);
        if (Stocks.TryGetValue(productId, out decimal s2)) return Task.FromResult(s2);
        return Task.FromResult(0m);
    }
}
