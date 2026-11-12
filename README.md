# Nalun POS Backend — SaaS Multi-Tenant

Backend empresarial de alta escalabilidad para el sistema de Punto de Venta (POS) **NalunPos**, construido sobre **.NET 10 (LTS)** con arquitectura preparada para operación SaaS multi-tenant, Row-Level Security (RLS) en SQL Server 2022 y desacoplamiento por capas.

---

## 🏗️ Stack Tecnológico

| Componente | Tecnología |
|---|---|
| **Plataforma** | .NET 10 (LTS) |
| **Base de datos** | SQL Server 2022 (EF Core 10 + Row-Level Security con `SESSION_CONTEXT`) |
| **Arquitectura** | Clean Architecture + DDD + CQRS (`IDispatcher` propio) + EDA / Outbox |
| **Mensajería** | RabbitMQ / EventBus desacoplado + Transactional Outbox con lease atómico |
| **IA** | Anti-Corruption Layer (provider-agnostic) |
| **Documentación API** | Scalar (`/scalar`) + OpenAPI nativo .NET 10 |
| **Seguridad** | JWT Bearer, Argon2id (`PasswordHasher`), RBAC + `HybridCache`, User Secrets (dev) |
| **Tests de Arquitectura** | ArchUnitNET + Roslyn Syntax Analysis (`Pos.Architecture.Tests`) |

---

## 📐 Arquitectura

```text
┌─────────────────────────────────────────────────────────┐
│                        Pos.Api                          │
│   Controllers • Middleware • Filters • OpenAPI/Scalar   │
└──────────────────────────┬──────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────┐
│                    Pos.Application                       │
│  CQRS (IDispatcher) • FluentValidation • Behaviors • AI │
│  Authentication • Users • Roles • Products • Sales      │
│  IntegrationEvents • Platform (SaaS) • AI Abstractions  │
└──────────────────────────┬──────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────┐
│                      Pos.Domain                         │
│   Entities • ValueObjects • DomainEvents • Interfaces   │
│   AggregateRoot • Entity<TId> • IDomainEvent            │
│   ITenantOwnedEntity • PlatformScope • Constants        │
└─────────────────────────────────────────────────────────┘
                           ▲
┌──────────────────────────┴──────────────────────────────┐
│                   Pos.Infrastructure                     │
│  Persistence (EF Core, Outbox, Migrations, RLS) • Auth  │
│  JwtTokenGenerator • HybridCache • AiOrchestration      │
│  Multitenancy • Observability • Persistence Readers     │
└─────────────────────────────────────────────────────────┘
```

### Regla de dependencias (validada automáticamente por ArchUnitNET)

```text
Pos.Api  →  Pos.Application  →  Pos.Domain
Pos.Infrastructure  →  Pos.Application  →  Pos.Domain
```

**Domain JAMÁS depende de:** EF Core, ASP.NET Core, RabbitMQ, Redis, SDKs de IA, ni ningún framework externo.

---

## 🗂️ Estructura de la Solución

```text
Pos.slnx
├── src/
│   ├── Pos.Api                 # Capa HTTP: controllers, middleware, background services, Scalar UI
│   ├── Pos.Application         # Casos de uso, CQRS propio (IDispatcher), behaviors, validadores
│   ├── Pos.Domain              # Dominio puro, libre de dependencias externas
│   └── Pos.Infrastructure      # EF Core, SQL Server RLS, JWT, HybridCache, Outbox, Persistence
├── tests/
│   ├── Pos.Domain.Tests        # Tests unitarios de dominio
│   ├── Pos.Application.Tests   # Tests unitarios de casos de uso y pipeline CQRS
│   ├── Pos.Infrastructure.Tests# Tests de infraestructura (multitenancy, JWT, outbox, seeders)
│   ├── Pos.Architecture.Tests  # Reglas de Clean Architecture y guardas Roslyn verificadas en suite
│   └── Pos.IntegrationTests    # Tests HTTP y E2E contra SQL Server real con RLS activo
├── docs/
│   ├── api/
│   │   └── openapi.v1.json     # Contrato OpenAPI v1 determinista y versionado (ADR 0024)
│   ├── developer-commands.md   # Comandos CLI oficiales (.NET 10, EF Core, User Secrets, OpenAPI, SQL Tests)
│   └── architecture/
│       ├── architecture.md     # Visión general de arquitectura
│       └── adr/                # 27 ADRs (0001–0024 e inventory-001–003)
├── Directory.Build.props       # Config global: TreatWarningsAsErrors, Nullable, LangVersion
├── Directory.Packages.props    # Central Package Management (todas las versiones aquí)
├── global.json                 # SDK .NET 10 fijado (evita drift entre máquinas/CI)
└── .editorconfig               # Consistencia de estilo entre desarrolladores
```

---

## 🚀 Inicio Rápido

### Pre-requisitos
- .NET 10 SDK (versión fijada en `global.json`)
- SQL Server (local o `(localdb)\mssqllocaldb`)
- Herramientas locales restauradas (`dotnet tool restore`) o `dotnet-ef`

### Configurar secretos de desarrollo (una vez por máquina)
```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\mssqllocaldb;Database=NalunPosDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;" --project src/Pos.Api
```

### Crear y migrar la base de datos
```powershell
dotnet ef database update --project src/Pos.Infrastructure --startup-project src/Pos.Api
```

### Ejecutar el backend
```powershell
dotnet run --project src/Pos.Api --launch-profile "https"
```

### Acceder a la documentación interactiva
Navega a `https://localhost:<puerto>/scalar` para la UI interactiva de la API.

---

## 🧪 Verificación de Calidad

```powershell
dotnet build -c Release Pos.slnx /p:TreatWarningsAsErrors=true
dotnet test tests/Pos.Domain.Tests/Pos.Domain.Tests.csproj -c Release --no-build
dotnet test tests/Pos.Application.Tests/Pos.Application.Tests.csproj -c Release --no-build
dotnet test tests/Pos.Infrastructure.Tests/Pos.Infrastructure.Tests.csproj -c Release --no-build
dotnet test tests/Pos.Architecture.Tests/Pos.Architecture.Tests.csproj -c Release --no-build
dotnet test tests/Pos.IntegrationTests/Pos.IntegrationTests.csproj -c Release --no-build
```

Las pruebas de `Pos.IntegrationTests` validan el flujo HTTP completo contra SQL Server real (LocalDB en desarrollo local o SQL Server 2022 en GitHub Actions CI) con políticas Row-Level Security (RLS) activas.

---

## 📋 Comandos Frecuentes

Ver referencia completa en [`docs/developer-commands.md`](docs/developer-commands.md).

| Acción | Comando |
|---|---|
| Nueva migración | `dotnet ef migrations add <Nombre> --project src/Pos.Infrastructure --startup-project src/Pos.Api` |
| Aplicar migración | `dotnet ef database update --project src/Pos.Infrastructure --startup-project src/Pos.Api` |
| Validar modelo EF | `dotnet ef migrations has-pending-model-changes --project src/Pos.Infrastructure --startup-project src/Pos.Api` |
| Drop de base de datos | `dotnet ef database drop --force --project src/Pos.Infrastructure --startup-project src/Pos.Api` |
| Listar secretos | `dotnet user-secrets list --project src/Pos.Api` |

---

## 🗺️ Hoja de Ruta Arquitectónica

| Fase | Estado | Descripción |
|---|---|---|
| **Fase 1** | ✅ Completa | Arquitectura base: Clean Architecture, CQRS propio (`IDispatcher`), EDA/Outbox, AI ACL, guardas arquitectónicas |
| **Fase 2** | ✅ Completa | Multi-tenancy SaaS: `ITenantOwnedEntity`, `Platform/`, Global Query Filters, SQL Server RLS (`TenantSessionContextInterceptor`), `TenantResolutionMiddleware`, contrato de roles y sesión |
| **Fase 3** | ✅ Completa | Observabilidad, contratos versionados (`v1`), background workers acotados (`ISystemExecutionScope`), Outbox concurrente con lease/dead-letter, caché tenant-aware con `HybridCache` y pruebas de integración SQL Server en CI |

---

## 📖 Decisiones de Arquitectura (ADRs)

| # | Decisión |
|---|---|
| [ADR 0001](docs/architecture/adr/0001-clean-architecture.md) | Clean Architecture y reglas de dependencia verificables |
| [ADR 0002](docs/architecture/adr/0002-modular-monolith.md) | Adopción de Monolito Modular |
| [ADR 0003](docs/architecture/adr/0003-event-driven-integration.md) | EventBus asíncrono y arquitectura orientada a eventos |
| [ADR 0004](docs/architecture/adr/0004-ai-anticorruption-layer.md) | ACL para capacidades de IA |
| [ADR 0005](docs/architecture/adr/0005-outbox-pattern.md) | Transactional Outbox para atomicidad |
| [ADR 0006](docs/architecture/adr/0006-module-physical-boundary-trigger.md) | Criterio de migración a assemblies por módulo |
| [ADR 0007](docs/architecture/adr/0007-ai-agent-action-governance.md) | Gobernanza de agentes autónomos de IA |
| [ADR 0008](docs/architecture/adr/0008-tenant-isolation-strategy.md) | Estrategia de aislamiento de tenant, RLS y sesión de autenticación |
| [ADR 0009](docs/architecture/adr/0009-saas-horizontal-scaling-path.md) | Camino de escalamiento horizontal SaaS |
| [ADR 0010](docs/architecture/adr/0010-integration-event-versioning-policy.md) | Política de versionado de Integration Events |
| [ADR 0011](docs/architecture/adr/0011-tenant-aware-cache-keys.md) | Claves de caché tenant-aware |
| [ADR 0012](docs/architecture/adr/0012-tenant-onboarding-model.md) | Modelo de onboarding de tenants |
| [ADR 0013](docs/architecture/adr/0013-dependency-licensing-policy.md) | Política de licenciamiento de dependencias — solo software libre |
| [ADR 0014](docs/architecture/adr/0014-commercial-dependency-replacements.md) | Sustituciones de dependencias comerciales — inventario de reemplazos |
| [ADR 0015](docs/architecture/adr/0015-revocacion-asincrona-roles.md) | Revocación asíncrona de roles y caché de permisos con `HybridCache` |
| [ADR 0016](docs/architecture/adr/0016-roslyn-en-tests-de-arquitectura.md) | Análisis sintáctico con Roslyn en tests de arquitectura |
| [ADR 0017](docs/architecture/adr/0017-centralized-tenant-requirement-behavior.md) | Requerimiento centralizado de tenant en pipeline CQRS e interceptor fail-closed |
| [ADR 0018](docs/architecture/adr/0018-estandares-2026-timeprovider-validateonstart-ci.md) | Estándares 2026 — `TimeProvider`, `ValidateOnStart` y gobernanza CI |
| [ADR 0019](docs/architecture/adr/0019-background-workers-rls-and-migrations-strategy.md) | Estrategia de migraciones (`efbundle`) y workers en segundo plano bajo RLS |
| [ADR 0020](docs/architecture/adr/0020-gestion-de-facturas-agotadas-y-evitacion-de-inanicion.md) | Gestión de facturas agotadas, prevención de inanición y alertas fiscales |
| [ADR 0021](docs/architecture/adr/0021-identidad-de-sistema-acotada-isystemexecutionscope.md) | Identidad de sistema acotada (`ISystemExecutionScope`) |
| [ADR 0022](docs/architecture/adr/0022-outbox-concurrente-lease-idempotencia-deadletter.md) | Outbox concurrente con lease atómico, idempotencia y dead-letter |
| [ADR 0023](docs/architecture/adr/0023-contrato-de-roles-y-sesion-de-autenticacion.md) | Contrato canónico de roles, resolución fail-closed y sesión de autenticación bajo RLS |
| [ADR 0024](docs/architecture/adr/0024-openapi-contract-snapshot-and-governance.md) | Snapshot determinista del contrato OpenAPI v1, gobernanza y detección de deriva |
| [ADR-Inventory-001](docs/architecture/adr/inventory-001-ledger-plus-materialized-quantity.md) | Ledger append-only + cantidad materializada en `StockLevel` |
| [ADR-Inventory-002](docs/architecture/adr/inventory-002-container-model-without-ui-trigger.md) | Contenedores (ubicaciones internas) — modelo de datos |
| [ADR-Inventory-003](docs/architecture/adr/inventory-003-optimistic-concurrency-stock.md) | Concurrencia optimista en `StockLevel` (`RowVersion`) |

---

## 🔐 Principios de Seguridad por Diseño

- **PCI-DSS por diseño:** ningún dato sensible de tarjeta (PAN, CVV) toca el dominio ni se persiste en la base de datos propia.
- **TenantId nunca desde el cliente:** se resuelve exclusivamente del JWT autenticado, del lado del servidor, y se refuerza en SQL Server mediante RLS (`SESSION_CONTEXT`).
- **Autorización y roles Fail-Closed:** los roles de sistema están reservados por constantes canónicas; `SuperAdmin` exige `RoleId == SuperAdminRoleId && TenantId == null`.
- **User Secrets en desarrollo:** las cadenas de conexión y claves JWT nunca se escriben en `appsettings.json` ni en el repositorio.
- **Warnings como errores:** configurado en `Directory.Build.props` (`TreatWarningsAsErrors=true`).
