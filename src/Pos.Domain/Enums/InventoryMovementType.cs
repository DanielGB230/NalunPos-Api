namespace Pos.Domain.Enums;

/// <summary>
/// Tipos de movimiento de inventario en el Kardex (append-only — ADR-Inventory-001).
/// Cada tipo tiene semántica y dirección de stock distinta.
/// </summary>
public enum InventoryMovementType
{
    Purchase = 1,               // Entrada por recepción de orden de compra
    Sale = 2,                   // Salida por venta comercial en POS
    Adjustment = 3,             // Ajuste manual con motivo (StockAdjustmentReason)
    ReturnFromCustomer = 4,     // Entrada — cliente devuelve mercadería
    ReturnToSupplier = 5,       // Salida — se devuelve mercadería al proveedor
    TransferOut = 6,            // Salida — traspaso desde este almacén hacia otro
    TransferIn = 7,             // Entrada — traspaso recibido desde otro almacén
}
