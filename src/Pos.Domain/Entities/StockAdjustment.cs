using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado Raíz para el Ajuste Manual de Stock.
///
/// Permite corregir el stock disponible con motivo documentado (StockAdjustmentReason).
/// Cada línea genera un InventoryMovement tipo Adjustment en Application Layer.
///
/// El ajuste es inmutable una vez creado — el Kardex registra el movimiento.
/// Una corrección a un ajuste previo se hace con un nuevo ajuste compensatorio.
/// </summary>
public class StockAdjustment : AggregateRoot<Guid>, ITenantOwnedEntity
{
    private readonly List<StockAdjustmentLine> _lines = [];

    public Guid TenantId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public StockAdjustmentReason Reason { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<StockAdjustmentLine> Lines => _lines.AsReadOnly();

    // Constructor privado para EF Core
    private StockAdjustment()
    {
    }

    private StockAdjustment(
        Guid id,
        Guid tenantId,
        Guid warehouseId,
        StockAdjustmentReason reason,
        IEnumerable<StockAdjustmentLine> lines,
        string? notes) : base(id)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("El ID del tenant es requerido para registrar un ajuste de stock.");

        if (warehouseId == Guid.Empty)
            throw new DomainException("El ID del almacén es requerido para registrar un ajuste de stock.");

        var linesList = lines?.ToList() ?? [];
        if (linesList.Count == 0)
            throw new DomainException("El ajuste de stock debe contener al menos una línea de producto.");

        TenantId = tenantId;
        WarehouseId = warehouseId;
        Reason = reason;
        Notes = notes?.Trim();
        _lines = linesList;
        CreatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new StockAdjustmentCreatedDomainEvent(Id, TenantId, WarehouseId, Reason, CreatedAtUtc));
    }

    public static StockAdjustment Create(
        Guid tenantId,
        Guid warehouseId,
        StockAdjustmentReason reason,
        IEnumerable<(Guid ProductId, decimal Quantity)> lineItems,
        string? notes = null)
    {
        var lines = lineItems
            .Select(i => StockAdjustmentLine.Create(i.ProductId, i.Quantity))
            .ToList();

        return new StockAdjustment(Guid.NewGuid(), tenantId, warehouseId, reason, lines, notes);
    }
}
