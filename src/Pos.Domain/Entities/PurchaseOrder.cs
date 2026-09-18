using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado Raíz para la Orden de Compra a Proveedores.
///
/// Ciclo de vida: Draft → Sent → PartiallyReceived → Received (final positivo)
///                              └──────────────────────────────→ Cancelled
///
/// Reemplaza la entidad Purchase (legacy) eliminada en ADR-Inventory-001.
/// A diferencia de Purchase, PurchaseOrder gestiona el ciclo de recepción parcial
/// y genera los InventoryMovement y StockLevel correspondientes a través del
/// ReceivePurchaseOrderCommandHandler (Application Layer).
///
/// NOTA: El efecto en stock NO ocurre en el dominio — ocurre en la Application Layer
/// para mantener la transacción atómica con StockLevel e InventoryMovement.
/// </summary>
public class PurchaseOrder : AggregateRoot<Guid>, ITenantOwnedEntity
{
    private readonly List<PurchaseOrderLine> _lines = [];

    public Guid TenantId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid WarehouseId { get; private set; }       // Destino del stock al recibir
    public string OrderNumber { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public PurchaseOrderStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<PurchaseOrderLine> Lines => _lines.AsReadOnly();

    /// <summary>Costo total de la orden (suma de todas las líneas por cantidad ordenada).</summary>
    public Money TotalOrderedCost => Money.Create(
        _lines.Sum(l => l.TotalCost.Amount),
        _lines.FirstOrDefault()?.UnitCost.Currency ?? "USD");

    // Constructor privado para EF Core
    private PurchaseOrder()
    {
    }

    private PurchaseOrder(
        Guid id,
        Guid tenantId,
        Guid supplierId,
        Guid warehouseId,
        string orderNumber,
        IEnumerable<PurchaseOrderLine> lines,
        string? notes) : base(id)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("El ID del tenant es requerido.");

        if (supplierId == Guid.Empty)
            throw new DomainException("El ID del proveedor es requerido para la orden de compra.");

        if (warehouseId == Guid.Empty)
            throw new DomainException("El ID del almacén destino es requerido para la orden de compra.");

        if (string.IsNullOrWhiteSpace(orderNumber))
            throw new DomainException("El número de orden de compra es requerido.");

        var linesList = lines?.ToList() ?? [];
        if (linesList.Count == 0)
            throw new DomainException("La orden de compra debe contener al menos una línea de producto.");

        TenantId = tenantId;
        SupplierId = supplierId;
        WarehouseId = warehouseId;
        OrderNumber = orderNumber.Trim().ToUpperInvariant();
        Notes = notes?.Trim();
        _lines = linesList;
        Status = PurchaseOrderStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new PurchaseOrderCreatedDomainEvent(Id, TenantId, SupplierId, WarehouseId, OrderNumber, CreatedAtUtc));
    }

    public static PurchaseOrder Create(
        Guid tenantId,
        Guid supplierId,
        Guid warehouseId,
        string orderNumber,
        IEnumerable<(Guid ProductId, decimal Quantity, Money UnitCost)> lineItems,
        string? notes = null)
    {
        var lines = lineItems
            .Select(i => PurchaseOrderLine.Create(i.ProductId, i.Quantity, i.UnitCost))
            .ToList();

        return new PurchaseOrder(Guid.NewGuid(), tenantId, supplierId, warehouseId, orderNumber, lines, notes);
    }

    /// <summary>
    /// Envía la orden al proveedor (Draft → Sent).
    /// No tiene efecto en el stock todavía.
    /// </summary>
    public void Send()
    {
        if (Status != PurchaseOrderStatus.Draft)
            throw new DomainException($"Solo se pueden enviar órdenes en estado Borrador. Estado actual: {Status}.");

        Status = PurchaseOrderStatus.Sent;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Registra la recepción de líneas (total o parcial).
    /// El efecto en StockLevel e InventoryMovement ocurre en ReceivePurchaseOrderCommandHandler.
    ///
    /// Retorna las líneas efectivamente actualizadas para que el handler genere los movimientos.
    /// </summary>
    public IReadOnlyList<PurchaseOrderLine> ReceiveLines(
        IEnumerable<(Guid ProductId, decimal ReceivedQuantity)> receivedItems)
    {
        if (Status == PurchaseOrderStatus.Draft)
            throw new DomainException("No se puede recibir mercadería de una orden en estado Borrador. Primero envíe la orden.");

        if (Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.Cancelled)
            throw new DomainException($"No se puede recibir mercadería de una orden en estado '{Status}'.");

        var updatedLines = new List<PurchaseOrderLine>();

        foreach (var (productId, receivedQty) in receivedItems)
        {
            var line = _lines.FirstOrDefault(l => l.ProductId == productId)
                ?? throw new DomainException($"El producto '{productId}' no existe en la orden de compra '{OrderNumber}'.");

            if (line.IsFullyReceived)
                throw new DomainException($"La línea del producto '{productId}' ya fue completamente recibida.");

            line.AddReceivedQuantity(receivedQty);
            updatedLines.Add(line);
        }

        // Actualiza el estado según cobertura de recepción
        Status = _lines.All(l => l.IsFullyReceived)
            ? PurchaseOrderStatus.Received
            : PurchaseOrderStatus.PartiallyReceived;

        UpdatedAtUtc = DateTime.UtcNow;

        if (Status == PurchaseOrderStatus.Received)
            RaiseDomainEvent(new PurchaseOrderReceivedDomainEvent(Id, OrderNumber, WarehouseId, DateTime.UtcNow));

        return updatedLines.AsReadOnly();
    }

    /// <summary>
    /// Cancela la orden. No tiene efecto en el stock si no hubo recepciones previas.
    /// Si hubo recepciones parciales, el stock ya fue incrementado y se mantiene.
    /// </summary>
    public void Cancel()
    {
        if (Status is PurchaseOrderStatus.Received)
            throw new DomainException("No se puede cancelar una orden completamente recibida.");

        if (Status == PurchaseOrderStatus.Cancelled)
            throw new DomainException("La orden ya se encuentra cancelada.");

        Status = PurchaseOrderStatus.Cancelled;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
