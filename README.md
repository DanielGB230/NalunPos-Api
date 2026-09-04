# Nalun POS Backend — v3 SaaS Multi-Tenant

Backend empresarial de alta escalabilidad para el sistema de Punto de Venta (POS) **NalunPos**, construido sobre **.NET 10 (LTS)** con arquitectura preparada para 20+ años de operación, evolución progresiva a SaaS multi-tenant y microservicios de Inteligencia Artificial.

---

## 🏗️ Stack Tecnológico

| Componente | Tecnología |
|---|---|
| **Plataforma** | .NET 10 (LTS) |
| **Base de datos** | SQL Server (EF Core 10) |
| **Arquitectura** | Clean Architecture + DDD + CQRS + EDA |
| **Mensajería** | RabbitMQ (preparado, no activado) |
| **IA** | Anti-Corruption Layer (preparado, provider-agnostic) |
| **Documentación API** | Scalar (`/scalar`) + OpenAPI nativo .NET 10 |
| **Seguridad** | JWT Bearer, BCrypt, User Secrets (dev) |
| **Tests de Arquitectura** | ArchUnitNET (valida dependencias en cada build) |

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
│  CQRS • MediatR • FluentValidation • Behaviors • AI ACL │
│  Authentication • Users • Products • Sales • Inventory  │
│  IntegrationEvents • Platform (SaaS) • AI Abstractions  │
└──────────────────────────┬──────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────┐
│                      Pos.Domain                         │
│   Entities • ValueObjects • DomainEvents • Interfaces   │
│   AggregateRoot • Entity<TId> • IDomainEvent            │
│   ITenantOwnedEntity (v3) • Constants • Exceptions      │
└─────────────────────────────────────────────────────────┘
                           ▲
┌──────────────────────────┴──────────────────────────────┐
│                   Pos.Infrastructure                     │
│  Persistence (EF Core, Outbox, Migrations) • Auth • JWT │
│  EventBus (RabbitMQ ready) • AiOrchestration • Caching  │
│  Multitenancy (Fase 2) • Observability (Fase 3) • Jobs  │
└─────────────────────────────────────────────────────────┘
```

### Regla de dependencias (validada automáticamente por ArchUnitNET)

```
Pos.Api  →  Pos.Application  →  Pos.Domain
Pos.Infrastructure  →  Pos.Application  →  Pos.Domain
```

**Domain JAMÁS depende de:** EF Core, ASP.NET Core, RabbitMQ, Redis, SDKs de IA, ni ningún framework externo.

---

## 🗂️ Estructura de la Solución

```text
Pos.slnx
├── src/
│   ├── Pos.Api                 # Capa HTTP: controllers, middleware, Scalar UI
│   ├── Pos.Application         # Casos de uso, CQRS, contratos de IA
│   ├── Pos.Domain              # Dominio puro, libre de dependencias
│   └── Pos.Infrastructure      # EF Core, JWT, RabbitMQ, Persistence
├── tests/
│   ├── Pos.Domain.Tests        # Tests unitarios de dominio
│   ├── Pos.Application.Tests   # Tests de casos de uso
│   ├── Pos.Infrastructure.Tests# Tests de infraestructura (InMemory DB)
│   └── Pos.Architecture.Tests  # Reglas de Clean Architecture verificadas en build
├── docs/
│   └── architecture/
│       ├── architecture.md     # Visión general v3
│       └── adr/               # 7 ADRs de decisiones arquitectónicas
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
- `dotnet-ef` tool global

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
dotnet build   # 0 errores, 0 advertencias (TreatWarningsAsErrors=true)
dotnet test    # Incluye Pos.Architecture.Tests que valida Clean Architecture
```

---

## 📋 Comandos Frecuentes

Ver referencia completa en [`docs/developer-commands.md`](docs/developer-commands.md).

| Acción | Comando |
|---|---|
| Nueva migración | `dotnet ef migrations add <Nombre> --project src/Pos.Infrastructure --startup-project src/Pos.Api` |
| Aplicar migración | `dotnet ef database update --project src/Pos.Infrastructure --startup-project src/Pos.Api` |
| Drop de base de datos | `dotnet ef database drop --force --project src/Pos.Infrastructure --startup-project src/Pos.Api` |
| Listar secretos | `dotnet user-secrets list --project src/Pos.Api` |

---

## 🗺️ Hoja de Ruta Arquitectónica

| Fase | Estado | Descripción |
|---|---|---|
| **Fase 1** | ✅ Completa | Arquitectura base: Clean Architecture, CQRS, EDA, AI ACL, 7 ADRs |
| **Fase 2** | 🔜 Pendiente | Multi-tenancy SaaS: `ITenantOwnedEntity`, `Platform/`, Global Query Filters, `TenantResolutionMiddleware` |
| **Fase 3** | 🔜 Pendiente | Observabilidad: OpenTelemetry, `CorrelationId`, contratos versionados (`V1/`), BackgroundJobs |

---

## 📖 Decisiones de Arquitectura (ADRs)

| # | Decisión |
|---|---|
| [ADR 0001](docs/architecture/adr/0001-clean-architecture.md) | Clean Architecture y reglas de dependencia verificables |
| [ADR 0002](docs/architecture/adr/0002-modular-monolith.md) | Adopción de Monolito Modular |
| [ADR 0003](docs/architecture/adr/0003-event-driven-integration.md) | EventBus asíncrono via RabbitMQ |
| [ADR 0004](docs/architecture/adr/0004-ai-anticorruption-layer.md) | ACL para capacidades de IA |
| [ADR 0005](docs/architecture/adr/0005-outbox-pattern.md) | Transactional Outbox para atomicidad |
| [ADR 0006](docs/architecture/adr/0006-module-physical-boundary-trigger.md) | Criterio de migración a assemblies por módulo |
| [ADR 0007](docs/architecture/adr/0007-ai-agent-action-governance.md) | Gobernanza de agentes autónomos de IA |
| ADR 0008 *(Fase 2)* | Estrategia de aislamiento de tenant |
| ADR 0009 *(Fase 2)* | Camino de escalamiento horizontal SaaS |
| ADR 0010 *(Fase 3)* | Política de versionado de Integration Events |
| ADR 0011 *(Fase 3)* | Claves de caché tenant-aware |

---

## 🔐 Principios de Seguridad por Diseño

- **PCI-DSS por diseño:** ningún dato sensible de tarjeta (PAN, CVV) toca el dominio ni se persiste en la base de datos propia.
- **TenantId nunca desde el cliente:** se resuelve exclusivamente del JWT autenticado, del lado del servidor.
- **User Secrets en desarrollo:** las cadenas de conexión y claves JWT nunca se escriben en `appsettings.json` ni en el repositorio.
- **Warnings como errores:** configurado en `Directory.Build.props` — ninguna deuda técnica silenciosa.
