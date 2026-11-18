# ADR 0026: Contrato de Tiempo — Instantes UTC con Sufijo `Z`, Regla de Nombres y Persistencia Fail-Closed

- **Estado:** Aceptado
- **Fecha:** 2026-11-17
- **Contexto:**
  1. La API serializa instantes con `System.Text.Json`, que emite el sufijo `'Z'` únicamente cuando el `DateTime` tiene `Kind=Utc`. Un `DateTime` con `Kind=Unspecified` se serializa sin zona (`"2026-11-16T22:44:52.4334821"`), y un cliente JavaScript lo interpreta como hora local, desplazando el instante según la zona del dispositivo.
  2. SQL Server (`datetime2`) no almacena el `Kind`: al leer, EF Core materializa `DateTime` con `Kind=Unspecified`. Sin conversión explícita, todo instante recuperado de la base pierde su naturaleza UTC, incluso si fue escrito correctamente.
  3. El defecto se observó en `GET /api/v1/platform/tenants` (`createdAtUtc` sin `'Z'`) mientras `login` (`expiresAtUtc`) sí emitía `'Z'`, porque el segundo valor se origina en memoria con `TimeProvider.GetUtcNow().UtcDateTime`.
  4. Un `ValueConverter` por configuración Fluent repetiría la misma lógica en más de veinte archivos y quedaría a merced de omisiones futuras; una convención centralizada es la única forma de que la garantía sea estructural.

---

## Decisión

### 1. Componente único y punto único de aplicación
- Se crea `Pos.Infrastructure/Persistence/UtcDateTimeConvention.cs` como **único** componente responsable del contrato de tiempo en persistencia.
- Se aplica en **un solo punto**: `PosDbContext.OnModelCreating`, después de `ApplyConfigurationsFromAssembly` y antes de los Global Query Filters.
- La convención recorre el modelo y aplica un `ValueConverter` a toda propiedad `DateTime` o `DateTime?` cuyo nombre termine en `Utc` (comparación `Ordinal`).

### 2. Regla de nombres
- Toda propiedad que represente un instante se llama `*Utc` (`CreatedAtUtc`, `OccurredAtUtc`, `TimestampUtc`, `ExpiresAtUtc`, …).
- Los DTOs, Responses y entidades de dominio respetan la misma regla: el sufijo comunica al consumidor que el valor es un instante absoluto.

### 3. Lectura: `Kind=Utc`
- Al recuperar un valor de la base, la conversión marca `Kind=Utc` si viene como `Unspecified` (caso normal de `datetime2`) y conserva el valor si ya es `Utc`.
- Un valor con `Kind=Local` recuperado de la base es un defecto grave de origen: la conversión lanza excepción en lugar de reinterpretarlo.

### 4. Escritura: fail-closed
- Si se intenta persistir un `DateTime` con `Kind != Utc`, la conversión lanza `InvalidOperationException` con un mensaje determinista que identifica la propiedad y el `Kind` recibido.
- No existe conversión silenciosa (ni `ToUniversalTime()` ni `SpecifyKind` en escritura): el instante debe construirse correctamente en origen con `TimeProvider.GetUtcNow().UtcDateTime`.
- El tipo de columna no cambia: el conversor es `DateTime` → `DateTime` (`ValueConverter<DateTime, DateTime>`) y `DateTime?` → `DateTime?`, por lo que **no genera migraciones**.

### 5. Excepción explícita: fechas de calendario
- `InventoryMovement.ExpirationDate` (`DateTime?`) es una fecha de vencimiento de lote, no un instante: se documenta como exclusión explícita y no se toca.
- Los filtros de calendario de los Requests (`StartDate`/`EndDate` en inventario y ventas, `ExpirationDate` en órdenes de compra) tampoco son instantes y quedan fuera del alcance de la convención y de la guarda de nombres.
- La convención no necesita una lista de exclusiones: al exigir el sufijo `Utc`, las fechas de calendario quedan fuera por diseño y sus configuraciones Fluent no se modifican.

### 6. Guardas de arquitectura (`Pos.Architecture.Tests`)
- `UtcTimeGovernanceTests.DateTimeProperties_InDomainAndDtos_MustBeNamedWithUtcSuffix`: audita por reflexión las propiedades `DateTime`/`DateTime?` de `Pos.Domain.Entities`, de los `*.DTOs` de `Pos.Application` y de `Pos.Api.Contracts.Responses`. Exige sufijo `Utc` o pertenencia a la lista de exclusión documentada (hoy solo `InventoryMovement.ExpirationDate`), y verifica que la lista no quede obsoleta.
- `UtcTimeGovernanceTests.EveryUtcProperty_InEfModel_HasUtcValueConverter`: sobre el modelo real de EF Core (`PosDbContext`), exige que toda propiedad `*Utc` tenga `ValueConverter` con el `ModelClrType` correcto.
- `Pos.Infrastructure.Tests.UtcDateTimeConventionTests`: cubre el comportamiento unitario del conversor (lectura `Unspecified` → `Utc`, escritura `Unspecified`/`Local` → excepción, nulos).
- `Pos.Infrastructure.Tests.UtcDateTimePersistenceTests` y `Pos.IntegrationTests.PlatformTenantProvisioningIntegrationTests.GetTenants_SerializesCreatedAtUtc_AsUtcInstantWithZSuffix`: pruebas contra SQL Server que reproducen el defecto (fallan sin la convención) y confirman el contrato de extremo a extremo.

### 7. Contrato HTTP
- `docs/api/openapi.v1.json` no cambia: los tipos siguen siendo `string` con formato `date-time`; el sufijo `'Z'` es un valor, no un cambio de esquema. El snapshot se verifica por SHA256 antes y después del cambio.

---

## Consecuencias

### Positivas
- Todo instante sale de la API con sufijo `'Z'` y se lee en el cliente como el mismo momento absoluto, sin depender de la zona horaria del navegador.
- La garantía es estructural: una propiedad nueva `*Utc` recibe el conversor automáticamente, y una propiedad de instante mal nombrada rompe la suite de arquitectura.
- Los errores de escritura se detectan en el punto de origen (fail-closed), no como desfases silenciosos en producción.
- Sin migraciones, sin cambios de esquema y sin dependencias nuevas.

### Negativas / Trade-offs
- `ExpirationDate` y los filtros de calendario siguen siendo `DateTime` sin sufijo: cualquier consumidor debe tratarlos como fechas, no como instantes (documentado aquí).
- Cualquier código que construya un `DateTime` de instante sin `Kind=Utc` fallará al guardar; es intencional y exige usar `TimeProvider`.
