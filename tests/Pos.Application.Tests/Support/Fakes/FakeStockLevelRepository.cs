namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Entities;
using Pos.Domain.Interfaces;

public sealed class FakeStockLevelRepository : IStockLevelRepository
{
    public List<StockLevel> StockLevels { get; } = [];
    public Task AddAsync(StockLevel stockLevel, CancellationToken cancellationToken = default) { StockLevels.Add(stockLevel); return Task.CompletedTask; }
    public Task<StockLevel?> GetAsync(Guid productId, Guid warehouseId, Guid? containerId, CancellationToken cancellationToken = default) => Task.FromResult(StockLevels.FirstOrDefault(s => s.ProductId == productId && s.WarehouseId == warehouseId && s.ContainerId == containerId));
    public Task<IReadOnlyList<StockLevel>> GetByWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StockLevel>>(StockLevels.Where(s => s.WarehouseId == warehouseId).ToList());
    public Task<IReadOnlyList<StockLevel>> GetBelowThresholdAsync(Guid? warehouseId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StockLevel>>(StockLevels.Where(s => s.QuantityAvailable <= s.MinStockThreshold).ToList());
    public void Update(StockLevel stockLevel) { }
}
