# Arquitectura del Backend POS SaaS — NalunPos v4

> **Historial de versiones:** v1 (arquitectura base Clean Architecture) → v2 (+ EventBus/Outbox, AI-ready, Architecture Tests, ADRs) → v3 (+ multi-tenancy SaaS, observabilidad, contratos versionados, jobs en segundo plano) → **v4 (+ modelo de onboarding vía SuperAdmin con ADR 0012, mapeo terminológico Control Plane/Application Plane de AWS, y arquitectura de dos frontends sobre un único backend y una única base de datos)**

---

## 1. Visión General

Este repositorio contiene la arquitectura base del backend para el sistema de Punto de Venta (POS) comercial **Pos**. Está construido sobre **.NET 10 (LTS)** y diseñado bajo los principios de **Clean Architecture**, **Domain-Driven Design (DDD)** táctico, **CQRS** selectivo y **Event-Driven Architecture (EDA)** asíncrono.

El objetivo fundamental es proveer un núcleo extremadamente limpio, seguro y desacoplado, con una expectativa de vida operativa de **20+ años**, capaz de evolucionar de forma transparente hacia un **SaaS multi-tenant** y microservicios de Inteligencia Artificial (IA) y agentes autónomos sin requerir reescrituras estructurales.

### Formato de solución: `.slnx` (decisión consciente)

La solución usa el formato moderno `.slnx` (XML slim, introducido en .NET 9+) en lugar del formato clásico `.sln`. Esta es una **decisión técnica consciente**, no accidental: el formato `.slnx` es el estándar oficial de .NET 10, es más legible, compatible con todas las herramientas de la plataforma (Visual Studio 2022+, VS Code, JetBrains Rider, `dotnet` CLI), y está libres de GUIDs innecesarios que generan conflictos frecuentes en equipos con Git. No se debe revertir a `.sln`.

---

## 2. Mapa de Capas y Regla de Dependencias

La arquitectura sigue una regla de dependencia unidireccional estricta hacia el centro (el Dominio):

```text
Pos.Api            Pos.Infrastructure
    ↓                     ↓
Pos.Application  ←────────┘
    ↓
Pos.Domain
```

### Principio de Inviolabilidad del Dominio

- **`Pos.Domain`** no tiene ninguna dependencia externa: proscripto Entity Framework Core, ASP.NET Core, RabbitMQ, Redis, SDKs de IA o librerías de terceros.
- **`Pos.Application`** solo depende de `Pos.Domain` y declara interfaces de servicios e IA (`AI/Abstractions`, `AI/Agents`, `Common/Interfaces/IEventBus`).
- **`Pos.Infrastructure`** implementa los detalles técnicos (EF Core, adaptadores de IA, RabbitMQ, persistencia, multi-tenancy).
- **`Pos.Api`** es una capa delgada encargada de la exposición HTTP, documentación OpenAPI nativa y configuración de inyección de dependencias.

Estas reglas son validadas automáticamente en cada `dotnet build` por `Pos.Architecture.Tests`. No se puede romper silenciosamente.

---

## 3. Estructura de Proyectos

### `src/Pos.Domain`

El corazón del sistema. Contiene el Kernel Compartido:
- `Common/Entity.cs`, `Common/AggregateRoot.cs`, `Common/ValueObject.cs`, `Common/IDomainEvent.cs`
- **v3:** `Common/ITenantOwnedEntity.cs` — marker interface para entidades de negocio con aislamiento de tenant *(Fase 2)*

### `src/Pos.Application`

Casos de uso y orquestación del sistema. Organizado verticalmente por funcionalidad (*vertical slicing*):

- **Módulos de negocio:** `Authentication`, `Users`, `Roles`, `Permissions`, `Products`, `Categories`, `Inventory`, `Suppliers`, `Purchases`, `Sales`, `Customers`, `CashRegisters`, `Payments`, `Invoicing`, `Branches`, `PosDevices`, `Notifications`, `Audit`.
- **`AI`:** Contratos de capacidades de IA (`Abstractions`) y acciones propuestas por agentes autónomos (`Agents`). Application *nunca conoce* un SDK de IA — solo el contrato.
- **`IntegrationEvents`:** Contratos de eventos asíncronos que cruzan límites del sistema, organizados bajo versiones (`V1/`, `V2/`...) *(Fase 3)*.
- **`Platform`:** Módulo exclusivo de SuperAdmin para gestión de tenants, planes y suscripciones *(Fase 2)*.

### `src/Pos.Infrastructure`

Detalles de implementación y adaptadores externos:
- **`Persistence`:** DbContext, repositorios, configuraciones de EF Core, migraciones en `Persistence/Migrations/` y patrón `Outbox`.
- **`Authentication`:** `JwtTokenGenerator`, `CurrentUserService`, `PasswordHasher`.
- **`EventBus`:** Adaptadores para RabbitMQ y brokers de mensajería.
- **`AiOrchestration`:** Adaptadores concretos hacia microservicios de IA y `AgentGovernance` para control de idempotencia, auditoría y Human-In-The-Loop.
- **`Multitenancy`:** Implementación de `ICurrentTenantContext`, `TenantSaveChangesInterceptor` *(Fase 2)*.
- **`Observability`:** Logging estructurado, tracing distribuido, `CorrelationContext` *(Fase 3)*.
- **`BackgroundJobs`:** Carpeta reservada para jobs recurrentes del SaaS *(Fase 3)*.

### `src/Pos.Api`

Capa de entrada HTTP delgada. Expone endpoints utilizando controllers y OpenAPI nativo (.NET 10) con documentación interactiva vía Scalar (`/scalar`).

- **`Controllers/Platform`:** Endpoints exclusivos de SuperAdmin *(Fase 2)*.
- **`Controllers/Tenant`:** Endpoints de negocio que requieren `TenantId` resuelto *(Fase 2)*.
- **`Middleware/TenantResolutionMiddleware`:** Único punto de resolución del `TenantId` a partir del JWT *(Fase 2)*.
- **`Middleware/CorrelationIdMiddleware`:** Genera o propaga el `CorrelationId` por request *(Fase 3)*.

### Arquitectura de Dos Frontends sobre un único Backend

**v4:** Dos proyectos de frontend separados consumen el mismo `Pos.Api` y la misma base de datos:

| Frontend | Audiencia | Endpoints consumidos |
|---|---|---|
| **`Pos.Admin.Web`** | SuperAdmin — gestión de plataforma, planes, monitoreo | `Controllers/Platform` |
| **`Pos.App.Web`** | TenantAdmin, Cajero, InventoryManager, Supervisor | `Controllers/Tenant` |

Esta separación es una decisión de UX/presentación, **no** una separación de backend ni de base de datos. Evita que el bundle del POS operativo cargue código del panel administrativo (y viceversa), y simplifica que cada frontend consuma solo los endpoints que le corresponden.

### `tests/Pos.Architecture.Tests`

Pruebas de arquitectura automatizadas (ArchUnitNET) que ejecutan en cada `dotnet build` para validar que las reglas de dependencia de Clean Architecture se respeten de forma inviolable. Son la primera defensa contra regresiones arquitectónicas.

---

## 4. Roles del Sistema, Contrato de Claims JWT y Modelo de Onboarding

*(Documentado en Fase 1 para evitar ambigüedad cuando se implemente Authentication en Fases 2+)*

### Roles iniciales (constantes en Domain)

| Rol | Nivel | TenantId en JWT |
|---|---|---|
| `SuperAdmin` | Plataforma | **Ausente / null** |
| `TenantAdmin` | Empresa | Presente y requerido |
| `Supervisor` | Empresa | Presente y requerido |
| `InventoryManager` | Empresa | Presente y requerido |
| `Cajero` | Empresa | Presente y requerido |

### Contrato de Claims mínimos del JWT

```json
{
  "sub": "<UserId>",
  "email": "<Email>",
  "role": "<NombreDelRol>",
  "tid": "<TenantId>",  // Ausente si rol == SuperAdmin
  "permission": ["<permission1>", "<permission2>"]
}
```

**Regla no negociable:** el `TenantId` **nunca se acepta desde el cliente** (body, query string, header). Se resuelve exclusivamente del lado del servidor a partir del JWT, en `TenantResolutionMiddleware`.

### Modelo de Onboarding de Tenants (v4)

**Decisión:** Alta asistida por SuperAdmin (`Api/Controllers/Platform`), no catálogo público self-service. Justificación y disparadores objetivos documentados en [ADR 0012](adr/0012-tenant-onboarding-model.md).

**Mapeo terminológico con AWS/Azure (Control Plane / Application Plane):** `Application/Platform/` es el equivalente funcional del **Control Plane** (gestiona los tenants mismos); los módulos de negocio con `ITenantOwnedEntity` son el **Application Plane**. Esta arquitectura los mantiene lógicamente separados pero físicamente unidos en un solo backend — la separación física es el camino de evolución documentado en el ADR 0009.

---

## 5. Estándares de Codificación

### Estandarización de Identificadores Únicos (Primary Keys)

**Estrictamente prohibido el uso de IDs numéricos secuenciales (int/long) para entidades de negocio. Toda entidad debe usar GUID (UUIDv7) como llave primaria.**

- **Seguridad:** Previene ataques de enumeración (IDOR) en la API.
- **Sincronización:** Habilita escenarios Offline-First y generación de identificadores en el cliente o nodo local sin colisiones.
- **Rendimiento en DB (.NET 10):** El uso de `Guid.CreateVersion7()` genera GUIDs ordenados por tiempo (sequential GUIDs), eliminando la fragmentación de índices B-Tree en SQL Server.

---

## 6. Registro de Decisiones de Arquitectura (ADRs)


Todas las decisiones estructurales están documentadas en `docs/architecture/adr/`:

### Fase 1 — Arquitectura Base

1. [ADR 0001: Clean Architecture y Reglas de Dependencia Verificables](adr/0001-clean-architecture.md)
2. [ADR 0002: Adopción de Monolito Modular (Modular Monolith)](adr/0002-modular-monolith.md)
3. [ADR 0003: Arquitectura Orientada a Eventos e Integración Asíncrona via RabbitMQ](adr/0003-event-driven-integration.md)
4. [ADR 0004: Capa Anti-Corrupción (ACL) para Capacidades de IA](adr/0004-ai-anticorruption-layer.md)
5. [ADR 0005: Garantía de Atomicidad con Transactional Outbox Pattern](adr/0005-outbox-pattern.md)
6. [ADR 0006: Disparadores Objetivos para Migración a Assemblies Físicos por Módulo](adr/0006-module-physical-boundary-trigger.md)
7. [ADR 0007: Gobernanza y Control de Acciones de Agentes Autónomos de IA](adr/0007-ai-agent-action-governance.md)

### Fase 2 — Multi-tenancy SaaS

8. [ADR 0008: Estrategia de Aislamiento de Tenant (Shared DB + Global Query Filters)](adr/0008-tenant-isolation-strategy.md)
9. [ADR 0009: Camino de Escalamiento Horizontal SaaS](adr/0009-saas-horizontal-scaling-path.md) *(actualizado v4 — mapeo AWS Control/Application Plane, dos frontends)*

### Fase 3 — Observabilidad y Contratos

10. [ADR 0010: Política de Versionado de Integration Events](adr/0010-integration-event-versioning-policy.md)
11. [ADR 0011: Claves de Caché Tenant-Aware](adr/0011-tenant-aware-cache-keys.md)

### Fase 4 — Modelo de Onboarding y Escalamiento (v4)

12. [ADR 0012: Modelo de Onboarding de Tenants (Alta Asistida vs. Self-Service)](adr/0012-tenant-onboarding-model.md)
