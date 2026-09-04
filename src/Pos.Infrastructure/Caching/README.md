# Reglas de Caché Tenant-Aware — Pos.Infrastructure

## Regla Estricta No Negociable

**Toda clave de caché que almacene datos de negocio DEBE incluir el `TenantId` como parte fundamental de la clave.**

### Formato estándar de clave:
`cache:{tenantId}:{modulo}:{entidad_o_recurso}`

### Ejemplos válidos:
- `cache:3fa85f64-5717-4562-b3fc-2c963f66afa6:products:list`
- `cache:3fa85f64-5717-4562-b3fc-2c963f66afa6:inventory:product:90a1`

### Ejemplos PROHIBIDOS (Vulnerabilidad de fuga de datos entre tenants):
- `cache:products:list` ❌
- `cache:inventory:product:90a1` ❌

### Excepciones autorizadas:
Únicamente cachés de plataforma globales que aplican por igual a todos los tenants (ej. `cache:platform:plans`, configuración global del sistema).

Ver: **ADR 0011 — Claves de Caché Tenant-Aware**
