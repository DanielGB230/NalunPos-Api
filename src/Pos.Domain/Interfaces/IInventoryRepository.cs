using Pos.Domain.Entities;
using Pos.Domain.Enums;

namespace Pos.Domain.Interfaces;

public interface IInventoryRepository
{
    /// <summary>Agrega un movimiento al Kardex (append-only).</summary>
    Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kardex paginado con filtros completos para reportes de auditoría.
    /// </summary>
    Task<(IReadOnlyList<InventoryMovement> Items, int TotalCount)> GetMovementsPagedAsync(
        Guid? productId,
        Guid? warehouseId,
        InventoryMovementType? movementType,
        DateTime? dateFrom,
        DateTime? dateTo,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calcula el stock de un producto en un almacén sumando el Kardex.
    /// Usado únicamente para reconciliación (fuente de verdad si StockLevel diverge).
    /// Para operaciones normales: usar IStockLevelRepository.GetAsync().
    /// </summary>
    Task<decimal> ReconcileStockFromLedgerAsync(
        Guid productId,
        Guid warehouseId,
        CancellationToken cancellationToken = default);
}
