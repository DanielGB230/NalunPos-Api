using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado Raíz para el Traspaso de Stock entre dos Almacenes.
///
/// INVARIANTE CRÍTICA (ADR-Inventory-001):
/// El traspaso genera EXACTAMENTE un par de movimientos de Kardex:
///   - InventoryMovement(TransferOut) en el almacén origen.
///   - InventoryMovement(TransferIn) en el almacén destino.
/// Ambos se crean en UNA SOLA transacción atómica en el CreateStockTransferCommandHandler.
/// El stock NUNCA queda "en el aire" entre ambos almacenes.
///
/// Visible solo cuando el tenant tiene 2+ Warehouses (el frontend lo controla).
/// </summary>
public class StockTransfer : AggregateRoot<Guid>, ITenantOwnedEntity
{
    private readonly List<StockTransferLine> _lines = [];

    public Guid TenantId { get; private set; }
    public Guid SourceWarehouseId { get; private set; }
    public Guid DestinationWarehouseId { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<StockTransferLine> Lines => _lines.AsReadOnly();

    // Constructor privado para EF Core
    private StockTransfer()
    {
    }

    private StockTransfer(
        Guid id,
        Guid tenantId,
        Guid sourceWarehouseId,
        Guid destinationWarehouseId,
        IEnumerable<StockTransferLine> lines,
        string? notes) : base(id)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("El ID del tenant es requerido para registrar un traspaso.");

        if (sourceWarehouseId == Guid.Empty)
            throw new DomainException("El almacén de origen es requerido para el traspaso.");

        if (destinationWarehouseId == Guid.Empty)
            throw new DomainException("El almacén de destino es requerido para el traspaso.");

        if (sourceWarehouseId == destinationWarehouseId)
            throw new DomainException("El almacén de origen y destino no pueden ser el mismo en un traspaso.");

        var linesList = lines?.ToList() ?? [];
        if (linesList.Count == 0)
            throw new DomainException("El traspaso debe contener al menos una línea de producto.");

        TenantId = tenantId;
        SourceWarehouseId = sourceWarehouseId;
        DestinationWarehouseId = destinationWarehouseId;
        Notes = notes?.Trim();
        _lines = linesList;
        CreatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new StockTransferCreatedDomainEvent(
            Id, TenantId, SourceWarehouseId, DestinationWarehouseId, CreatedAtUtc));
    }

    public static StockTransfer Create(
        Guid tenantId,
        Guid sourceWarehouseId,
        Guid destinationWarehouseId,
        IEnumerable<(Guid ProductId, decimal Quantity)> lineItems,
        string? notes = null)
    {
        var lines = lineItems
            .Select(i => StockTransferLine.Create(i.ProductId, i.Quantity))
            .ToList();

        return new StockTransfer(Guid.NewGuid(), tenantId, sourceWarehouseId, destinationWarehouseId, lines, notes);
    }
}
