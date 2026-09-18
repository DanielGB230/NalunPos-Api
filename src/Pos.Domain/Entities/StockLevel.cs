using Pos.Domain.Common;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Entities;

/// <summary>
/// Proyección materializada del stock por producto × almacén × (ubicación opcional).
///
/// REGLA ARQUITECTÓNICA (ADR-Inventory-001):
/// - StockLevel es el CACHÉ de lectura rápida. La fuente de verdad es InventoryMovement (ledger).
/// - Nunca se escribe en StockLevel sin escribir el InventoryMovement correspondiente
///   en la misma transacción atómica.
/// - Si StockLevel y SUM(InventoryMovement.Quantity) divergen: recalcular StockLevel
///   desde el ledger — nunca al revés.
///
/// CONCURRENCIA (ADR-Inventory-003):
/// - RowVersion garantiza concurrencia optimista.
/// - Si dos transacciones chocan, EF Core lanza DbUpdateConcurrencyException.
/// - El handler reintenta la lectura del StockLevel actualizado antes de reintentar
///   la operación — nunca "última escritura gana" silenciosa.
///
/// SALDOS:
/// - QuantityAvailable: stock libre para vender o transferir.
/// - QuantityReserved: stock comprometido (ej: orden pendiente) — no disponible para venta.
/// - TotalPhysical: suma física real en el almacén (QuantityAvailable + QuantityReserved).
/// - MinStockThreshold: umbral de alerta de stock bajo (configurable por producto/almacén).
/// </summary>
public class StockLevel : Entity<Guid>, ITenantOwnedEntity
{
    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid? ContainerId { get; private set; }          // ADR-Inventory-002: nullable, sin UI por ahora

    public decimal QuantityAvailable { get; private set; }  // Stock libre para operaciones
    public decimal QuantityReserved { get; private set; }   // Stock comprometido (pedidos pendientes)
    public decimal MinStockThreshold { get; private set; }  // Umbral para alerta de stock bajo

    /// <summary>Stock físico total en el almacén (disponible + reservado).</summary>
    public decimal TotalPhysical => QuantityAvailable + QuantityReserved;

    /// <summary>True cuando QuantityAvailable es igual o menor al umbral configurado.</summary>
    public bool IsBelowMinThreshold => QuantityAvailable <= MinStockThreshold;

    /// <summary>
    /// Token de concurrencia optimista (ADR-Inventory-003).
    /// Mapeado como IsRowVersion() en EF Core — se incrementa automáticamente en cada UPDATE.
    /// EF Core lanza DbUpdateConcurrencyException si el valor de la BD no coincide al guardar.
    /// </summary>
    public byte[] RowVersion { get; private set; } = [];

    // Constructor privado para EF Core
    private StockLevel()
    {
    }

    private StockLevel(
        Guid id,
        Guid tenantId,
        Guid productId,
        Guid warehouseId,
        Guid? containerId,
        decimal minStockThreshold) : base(id)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("El ID del tenant es requerido para crear un nivel de stock.");

        if (productId == Guid.Empty)
            throw new DomainException("El ID del producto es requerido para crear un nivel de stock.");

        if (warehouseId == Guid.Empty)
            throw new DomainException("El ID del almacén es requerido para crear un nivel de stock.");

        TenantId = tenantId;
        ProductId = productId;
        WarehouseId = warehouseId;
        ContainerId = containerId;
        QuantityAvailable = 0m;
        QuantityReserved = 0m;
        MinStockThreshold = minStockThreshold >= 0
            ? minStockThreshold
            : throw new DomainException("El umbral mínimo de stock no puede ser negativo.");
    }

    public static StockLevel Create(
        Guid tenantId,
        Guid productId,
        Guid warehouseId,
        Guid? containerId = null,
        decimal minStockThreshold = 0m)
    {
        return new StockLevel(Guid.NewGuid(), tenantId, productId, warehouseId, containerId, minStockThreshold);
    }

    /// <summary>
    /// Incrementa el stock disponible (entradas: compras, devoluciones de clientes, traspasos entrantes).
    /// </summary>
    public void Increment(decimal quantity)
    {
        if (quantity <= 0)
            throw new DomainException($"La cantidad a incrementar debe ser mayor a cero. Recibido: {quantity}.");

        QuantityAvailable += quantity;
    }

    /// <summary>
    /// Decrementa el stock disponible (salidas: ventas, traspasos salientes, devoluciones a proveedor).
    /// Lanza DomainException si el stock disponible es insuficiente.
    /// </summary>
    public void Decrement(decimal quantity)
    {
        if (quantity <= 0)
            throw new DomainException($"La cantidad a decrementar debe ser mayor a cero. Recibido: {quantity}.");

        if (QuantityAvailable < quantity)
            throw new DomainException(
                $"Stock insuficiente. Disponible: {QuantityAvailable}, solicitado: {quantity}.");

        QuantityAvailable -= quantity;
    }

    /// <summary>
    /// Reserva stock: mueve cantidad de Disponible → Reservado.
    /// Se usa cuando una orden está confirmada pero no despachada.
    /// </summary>
    public void Reserve(decimal quantity)
    {
        if (quantity <= 0)
            throw new DomainException("La cantidad a reservar debe ser mayor a cero.");

        if (QuantityAvailable < quantity)
            throw new DomainException(
                $"No hay suficiente stock disponible para reservar. Disponible: {QuantityAvailable}, solicitado: {quantity}.");

        QuantityAvailable -= quantity;
        QuantityReserved += quantity;
    }

    /// <summary>
    /// Libera una reserva: devuelve la cantidad de Reservado → Disponible.
    /// Se usa al cancelar una orden o al liberar una reserva que no se despachó.
    /// </summary>
    public void ReleaseReservation(decimal quantity)
    {
        if (quantity <= 0)
            throw new DomainException("La cantidad a liberar debe ser mayor a cero.");

        if (QuantityReserved < quantity)
            throw new DomainException(
                $"No se puede liberar más stock del que está reservado. Reservado: {QuantityReserved}, liberando: {quantity}.");

        QuantityReserved -= quantity;
        QuantityAvailable += quantity;
    }

    /// <summary>
    /// Confirma el despacho de stock reservado (elimina la reserva al materializarse la salida).
    /// No mueve cantidad a Disponible — el stock físicamente salió.
    /// </summary>
    public void ConfirmDispatch(decimal quantity)
    {
        if (quantity <= 0)
            throw new DomainException("La cantidad a despachar debe ser mayor a cero.");

        if (QuantityReserved < quantity)
            throw new DomainException(
                $"No se puede despachar más stock del que está reservado. Reservado: {QuantityReserved}, despachando: {quantity}.");

        QuantityReserved -= quantity;
    }

    /// <summary>
    /// Actualiza el umbral mínimo de stock para alertas de reposición.
    /// </summary>
    public void SetMinStockThreshold(decimal threshold)
    {
        if (threshold < 0)
            throw new DomainException("El umbral mínimo de stock no puede ser negativo.");

        MinStockThreshold = threshold;
    }
}
