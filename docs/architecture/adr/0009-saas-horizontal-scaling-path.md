# ADR 0009: Camino de Escalamiento Horizontal SaaS

- **Estado:** Aceptado (actualizado v4)
- **Fecha:** 2026-09-01 | **Revisado:** 2026-09-03
- **Contexto:** Definir el plan de escalamiento futuro a medida que crezca el número de tenants y el volumen de datos en la plataforma SaaS.

## Mapeo Terminológico con AWS / Azure (referencia para documentación externa)

AWS SaaS Factory y el whitepaper "SaaS Architecture Fundamentals" describen esta misma separación con los términos **Control Plane** (gestiona los tenants mismos, no es multi-tenant) y **Application Plane** (la funcionalidad de negocio, sí multi-tenant).

En este sistema el mapeo es:

| Término AWS | Equivalente en este proyecto |
|---|---|
| **Control Plane** | `Application/Platform/` (gestión de Tenants, Planes, Suscripciones) |
| **Application Plane** | Módulos de negocio (`Sales`, `Inventory`, etc.) con `ITenantOwnedEntity` |

AWS despliega ambos planos como servicios físicamente separados a gran escala (AWS SaaS Builder Toolkit). Esta arquitectura los mantiene **lógicamente separados pero físicamente unidos** en un solo backend (`Pos.Api`), lo que es la decisión correcta al volumen actual. La separación física queda como camino de evolución documentado en este ADR.

## Dos Frontends desde el Diseño Inicial

`Pos.Admin.Web` (panel de SuperAdmin — gestión de plataforma, planes, monitoreo) y `Pos.App.Web` (panel operativo del negocio — Cajero, InventoryManager, Supervisor) nacen como proyectos de frontend distintos desde el día 1, ambos consumiendo el mismo `Pos.Api`.

Esto **no** es escalamiento — es una decisión de UX/presentación que evita mezclar audiencias sin implicar separación de backend ni de base de datos. Cada frontend consume exclusivamente los endpoints que le corresponden (`Controllers/Platform` el panel de SuperAdmin, `Controllers/Tenant` el panel operativo).

## Decisión — Disparadores de Escalamiento

1. **Backend stateless:** La API opera sin estado de sesión en memoria (JWT), permitiendo réplicas horizontales via balanceadores de carga sin cambios de arquitectura.
2. **Separación del backend de plataforma (Control Plane físico):** Disparador — cuando el volumen de operaciones de plataforma (soporte, monitoreo, facturación de suscripciones) justifique que `Application/Platform` se despliegue como servicio independiente de `Pos.Api` con su propio ciclo de release. Mientras ese disparador no ocurra, un solo backend es la decisión correcta.
3. **Migración híbrida database-per-tenant:** Disparador — cuando un tenant específico exija aislamiento físico por contrato, compliance, o volumen extremo. La migración es posible sin cambiar el modelo de aplicación gracias a que el acceso a datos pasa siempre por `ICurrentTenantContext`.
4. **Extracción a microservicios por módulo:** Gobernado por los disparadores definidos en el ADR 0006.

## Consecuencias

- **Positivas:** Clara hoja de ruta de crecimiento con disparadores objetivos; sin decisiones prematuras ni sobreingeniería.
- **Negativas:** Monitoreo constante de métricas de carga y base de datos para activar los disparadores a tiempo.
