# ADR 0011: Claves de Caché Tenant-Aware

- **Estado:** Aceptado
- **Fecha:** 2026-09-01
- **Contexto:** En una arquitectura SaaS multi-tenant con base de datos compartida, la caché en memoria o distribuida (Redis) puede convertirse en un punto de fuga de datos si dos tenants comparten una misma clave de caché.

## Decisión
Establecer como regla de seguridad estricta que **toda clave de caché que almacene datos de negocio debe incluir obligatoriamente el `TenantId` resuelto del contexto actual**:

1. **Formato Obligatorio:** `cache:{tenantId}:{modulo}:{recurso}`.
2. **Excepciones Únicas:** Cachés de plataforma explícitamente globales (ej. lista de planes comerciales, banderas de características del sistema).
3. **Verificación:** Code reviews e implementaciones de decoradores de caché deben hacer cumplir esta política.

## Consecuencias
- **Positivas:** Elimina el riesgo de contaminación cruzada o fuga de información confidencial entre tenants a través de la capa de almacenamiento en caché.
- **Negativas:** Las claves de caché son ligeramente más extensas.
