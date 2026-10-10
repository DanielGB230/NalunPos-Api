namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;

public sealed class FakePurchaseOrderRepository : IPurchaseOrderRepository
{
    public List<PurchaseOrder> Orders { get; } = [];
    public Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Orders.FirstOrDefault(o => o.Id == id));
    public Task<PurchaseOrder?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default) => Task.FromResult(Orders.FirstOrDefault(o => o.OrderNumber == orderNumber));
    public Task AddAsync(PurchaseOrder order, CancellationToken cancellationToken = default) { Orders.Add(order); return Task.CompletedTask; }
    public void Update(PurchaseOrder order) { }
    public Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, PurchaseOrderStatus? status = null, Guid? supplierId = null, Guid? warehouseId = null, CancellationToken cancellationToken = default)
    {
        var filtered = Orders
            .Where(o => (!status.HasValue || o.Status == status.Value)
                     && (!supplierId.HasValue || o.SupplierId == supplierId.Value)
                     && (!warehouseId.HasValue || o.WarehouseId == warehouseId.Value))
            .ToList();
        var items = filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult<(IReadOnlyList<PurchaseOrder>, int)>((items, filtered.Count));
    }
}
