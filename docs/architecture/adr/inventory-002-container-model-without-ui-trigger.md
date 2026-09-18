# ADR-Inventory-002: Contenedores (Ubicaciones Internas) — Modelo de Datos sin UI Inicial

## Estado
Aceptado

## Contexto
En grandes almacenes o centros de distribución, el stock no solo reside en un almacén general sino en pasillos, estantes, bins o palets específicos (denominados `Container`).
Sin embargo, el 90% de los clientes o tenants iniciales de NalunPOS son pequeños comercios que manejan un único almacén sin subdivisiones internas de ubicación. Rediseñar la base de datos en el futuro para agregar `ContainerId` requeriría migraciones destructivas y cambios masivos en las consultas.

## Decisión
1. Incorporar la entidad `Container` en la capa de Dominio e Infraestructura desde el día uno.
2. Agregar la propiedad nullable `ContainerId (Guid?)` en `StockLevel` y en `InventoryMovement`.
3. Para la versión inicial y mientras no exista una solicitud explícita de un tenant de gran escala, **no se construirá interfaz gráfica (UI) para la gestión de contenedores**.
4. Todos los flujos por defecto utilizarán `ContainerId = null`, lo que indica "Almacén General / Stock No Subdividido".

## Consecuencias
- **Positivas:**
  - El modelo de dominio e infraestructura queda 100% preparado para soportar ubicaciones internas multinivel (pasillos, estantes) sin cambios de esquema ni migraciones futuras.
  - Cero complejidad visual o cognitiva para los usuarios de pequeñas empresas (1 almacén sin ubicaciones complejas).
- **Negativas:**
  - Columna adicional nullable en la base de datos (`ContainerId`), con impacto insignificante en almacenamiento.
