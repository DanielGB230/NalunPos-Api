using Pos.Domain.Entities;

namespace Pos.Domain.Interfaces;

public interface IStockLevelRepository
{
    Task AddAsync(StockLevel stockLevel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el StockLevel por la clave de negocio única (ProductId + WarehouseId + ContainerId).
    /// Usa tracking para concurrencia optimista (RowVersion).
    /// </summary>
    Task<StockLevel?> GetAsync(
        Guid productId,
        Guid warehouseId,
        Guid? containerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene todos los niveles de stock de un almacén (para la vista de stock actual).
    /// </summary>
    Task<IReadOnlyList<StockLevel>> GetByWarehouseAsync(
        Guid warehouseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los niveles de stock por debajo del umbral mínimo configurado.
    /// </summary>
    Task<IReadOnlyList<StockLevel>> GetBelowThresholdAsync(
        Guid? warehouseId,
        CancellationToken cancellationToken = default);

    void Update(StockLevel stockLevel);
}
