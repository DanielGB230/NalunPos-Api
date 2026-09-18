namespace Pos.Domain.Enums;

/// <summary>
/// Unidad de medida del producto en el catálogo.
/// Controla cómo se registra y reporta el stock.
/// </summary>
public enum UnitOfMeasure
{
    Unit = 1,    // Unidad (ej: producto individual)
    Kg = 2,      // Kilogramo
    Liter = 3,   // Litro
    Box = 4,     // Caja o bulto
    Gram = 5,    // Gramo
    Meter = 6,   // Metro lineal
    Pack = 7,    // Pack / multipack
}

/// <summary>
/// Motivo del ajuste manual de stock.
/// Valor obligatorio para garantizar trazabilidad de la razón del movimiento.
/// </summary>
public enum StockAdjustmentReason
{
    Shrinkage = 1,      // Merma (pérdida natural, vencimiento)
    PhysicalCount = 2,  // Conteo físico / inventario periódico
    Correction = 3,     // Corrección de error de carga
    Donation = 4,       // Donación o salida voluntaria
    Breakage = 5,       // Rotura o daño físico
}

/// <summary>
/// Ciclo de vida de una Orden de Compra (reemplaza PurchaseStatus legacy).
/// </summary>
public enum PurchaseOrderStatus
{
    Draft = 1,              // Borrador — sin efecto en stock
    Sent = 2,               // Enviada al proveedor — sin efecto en stock todavía
    PartiallyReceived = 3,  // Recepción parcial — stock incrementado por líneas recibidas
    Received = 4,           // Recepción total — orden cerrada
    Cancelled = 5,          // Cancelada — sin efecto en stock
}
