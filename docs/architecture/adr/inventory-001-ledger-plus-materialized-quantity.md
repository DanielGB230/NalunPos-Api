# ADR-Inventory-001: Ledger Append-Only + Cantidad Materializada en StockLevel

## Estado
Aceptado

## Contexto
En versiones anteriores de NalunPOS, el stock de productos residía directamente en la propiedad `Product.StockQuantity` y se actualizaba mutando ese campo en el agregado de Producto. Este enfoque plano presenta problemas severos de escalabilidad:
1. No soporta múltiples sucursales ni múltiples almacenes por empresa (tenant).
2. No ofrece auditabilidad de por qué cambió el stock (compras, ventas, ajustes, traspasos, pérdidas).
3. Provoca cuellos de botella de concurrencia al bloquear la tabla de Productos en cada venta o movimiento de inventario.

## Decisión
Implementar un modelo dual **Ledger + Cantidad Materializada (Caché)**:

1. **`InventoryMovement` (Ledger / Kardex):**
   - Es una bitácora **append-only** (inmutable). Los registros nunca se actualizan ni se eliminan.
   - Todo cambio físico de stock genera una fila en `InventoryMovements` especificando `ProductId`, `WarehouseId`, `Quantity` (positiva o negativa), `MovementType`, `ReferenceId` y `OccurredAtUtc`.

2. **`StockLevel` (Proyección Materializada / Caché):**
   - Mantiene la suma acumulada de stock por tupla única `(ProductId, WarehouseId, ContainerId)`.
   - Ofrece consultas `O(1)` de alta velocidad para validación de stock disponible antes de ventas.
   - Expone métodos de dominio `Increment(qty)` y `Decrement(qty)` con validación de saldos (`QuantityAvailable`, `QuantityReserved`, `MinStockThreshold`).

3. **Garantía de Atomicidad Transaccional:**
   - La mutación de `StockLevel` y la inserción del `InventoryMovement` **deben ocurrir siempre en la misma transacción atómica de EF Core** (`SaveChangesAsync` único).
   - Si por algún fallo `StockLevel` y la suma del Kardex llegaran a divergir, la fuente de verdad es la suma de `InventoryMovement` (reconciliación autónoma).

## Consecuencias
- **Positivas:**
  - Auditabilidad y trazabilidad 100% de todo movimiento de inventario.
  - Soporte multi-almacén nativo y sin reestructuración futura.
  - Consultas de lectura ultra-rápidas en el TPV (Punto de Venta) sin agregar el Kardex en tiempo real.
- **Negativas:**
  - Mayor espacio en disco por el crecimiento continuo de la tabla `InventoryMovements`.
