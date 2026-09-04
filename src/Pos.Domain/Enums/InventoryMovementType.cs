namespace Pos.Domain.Enums;

/// <summary>
/// Tipos de movimiento de inventario en el Kardex.
/// </summary>
public enum InventoryMovementType
{
    Purchase = 1,   // Entrada por compra a proveedor
    Sale = 2,       // Salida por venta comercial
    Adjustment = 3, // Ajuste manual de inventario (positivo o negativo)
    Return = 4      // Devolución de cliente o proveedor
}
