using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado inmutable (Append-Only / Kardex) para registrar cada movimiento físico de inventario.
///
/// REGLA ARQUITECTÓNICA (ADR-Inventory-001):
/// - Los registros del Kardex NUNCA se modifican ni eliminan una vez confirmados.
/// - Una corrección se efectúa mediante un nuevo movimiento compensatorio.
/// - WarehouseId es REQUERIDO: el Kardex tiene visibilidad por almacén.
/// - ContainerId es OPCIONAL (ADR-Inventory-002): soporte para ubicaciones internas sin UI actual.
/// - BatchNumber y ExpirationDate permiten trazabilidad de lotes y vencimientos.
/// </summary>
public class InventoryMovement : AggregateRoot<Guid>, ITenantOwnedEntity
{
    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid? ContainerId { get; private set; }
    public decimal Quantity { get; private set; }       // Positivo = entrada, Negativo = salida
    public InventoryMovementType MovementType { get; private set; }
    public Guid? ReferenceId { get; private set; }      // SaleId / PurchaseOrderId / AdjustmentId / TransferId
    public string? Notes { get; private set; }
    public string? BatchNumber { get; private set; }    // Número de lote (trazabilidad)
    public DateTime? ExpirationDate { get; private set; }   // Fecha de vencimiento del lote
    public DateTime OccurredAtUtc { get; private set; }     // Cuándo ocurrió el evento físico
    public DateTime CreatedAtUtc { get; private set; }      // Cuándo se registró en sistema

    // Constructor privado para EF Core
    private InventoryMovement()
    {
    }

    private InventoryMovement(
        Guid id,
        Guid productId,
        Guid warehouseId,
        Guid? containerId,
        decimal quantity,
        InventoryMovementType movementType,
        Guid? referenceId,
        string? notes,
        string? batchNumber,
        DateTime? expirationDate,
        DateTime? occurredAtUtc) : base(id)
    {
        if (productId == Guid.Empty)
            throw new DomainException("El ID del producto es requerido para registrar un movimiento de inventario.");

        if (warehouseId == Guid.Empty)
            throw new DomainException("El ID del almacén es requerido para registrar un movimiento de inventario.");

        if (quantity == 0)
            throw new DomainException("La cantidad del movimiento de inventario no puede ser cero.");

        ProductId = productId;
        WarehouseId = warehouseId;
        ContainerId = containerId;
        Quantity = quantity;
        MovementType = movementType;
        ReferenceId = referenceId;
        Notes = notes?.Trim();
        BatchNumber = batchNumber?.Trim().ToUpperInvariant();
        ExpirationDate = expirationDate;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        CreatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new InventoryMovementRecordedDomainEvent(
            Id,
            ProductId,
            WarehouseId,
            Quantity,
            MovementType,
            ReferenceId,
            OccurredAtUtc));
    }

    /// <summary>
    /// Registra un movimiento en el Kardex. Cantidad positiva = entrada, negativa = salida.
    /// </summary>
    public static InventoryMovement Record(
        Guid productId,
        Guid warehouseId,
        decimal quantity,
        InventoryMovementType movementType,
        Guid? referenceId = null,
        string? notes = null,
        Guid? containerId = null,
        string? batchNumber = null,
        DateTime? expirationDate = null,
        DateTime? occurredAtUtc = null)
    {
        return new InventoryMovement(
            Guid.NewGuid(),
            productId,
            warehouseId,
            containerId,
            quantity,
            movementType,
            referenceId,
            notes,
            batchNumber,
            expirationDate,
            occurredAtUtc);
    }
}
