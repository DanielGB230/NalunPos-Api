# ADR-Inventory-003: Concurrencia Optimista en StockLevel (`RowVersion`)

## Estado
Aceptado

## Contexto
En un sistema Punto de Venta (POS) multi-caja o en e-commerce con múltiples terminales vendiendo simultáneamente, dos o más transacciones pueden intentar decrementar o incrementar el stock del mismo producto en el mismo almacén al mismo milisegundo.
Sin un control estricto de concurrencia, la última escritura sobrescribiría a la anterior ("última escritura gana" silenciosa), corrompiendo la cantidad materializada y provocando descuadres de inventario.

## Decisión
1. Mapear la propiedad `RowVersion (byte[])` en la entidad `StockLevel` utilizando la anotación Fluent API `builder.Property(s => s.RowVersion).IsRowVersion();`.
2. En SQL Server, EF Core mapea `IsRowVersion()` a una columna de tipo `rowversion` (`timestamp`), la cual es auto-incrementada a nivel de motor de base de datos en cada `UPDATE`.
3. Cuando ocurre una actualización concurrente conflictiva, EF Core incluye `WHERE RowVersion = @originalRowVersion` en la sentencia `UPDATE`. Si el valor cambió, la consulta afecta 0 filas y EF Core lanza una `DbUpdateConcurrencyException`.
4. El sistema **nunca** ignorará ni sobrescribirá silenciosamente un conflicto de concurrencia. La transacción fallará de forma limpia retornando un error de tipo `Conflict` ("Se detectó un conflicto de concurrencia al actualizar el stock. Reintente la operación").

## Consecuencias
- **Positivas:**
  - Garantía del 100% de consistencia de saldos en entornos altamente concurrentes (múltiples cajas registradoras).
  - Eliminación absoluta de sobreescrituras silenciosas de stock.
- **Negativas:**
  - En escenarios de altísima contención sobre un único producto, las transacciones perdedoras deberán ser reintentadas por el cliente o la aplicación.
