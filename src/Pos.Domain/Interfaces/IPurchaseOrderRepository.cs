using Pos.Domain.Entities;
using Pos.Domain.Enums;

namespace Pos.Domain.Interfaces;

public interface IPurchaseOrderRepository
{
    Task AddAsync(PurchaseOrder order, CancellationToken cancellationToken = default);
    Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        PurchaseOrderStatus? status = null,
        Guid? supplierId = null,
        Guid? warehouseId = null,
        CancellationToken cancellationToken = default);
    void Update(PurchaseOrder order);
}
