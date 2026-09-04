# ADR 0014: Sustituciones de Dependencias Comerciales — Inventario de Reemplazos

- **Estado:** Aceptado
- **Fecha:** 2026-09-03
- **Contexto:** Aplicación del ADR 0013. Registro de las dependencias comerciales detectadas y sus reemplazos libres aprobados.

## Inventario de Dependencias y Estado

| Paquete | Licencia original | Cambio de licencia | Estado en este proyecto | Reemplazo aprobado |
|---|---|---|---|---|
| **MediatR** | MIT (≤ v12) | v13: licencia comercial (jul 2025) | ❌ **Eliminado** | Dispatcher CQRS propio en `Application/Common/Dispatching/` |
| **AutoMapper** | MIT (≤ v12) | v13: licencia comercial | ✅ Nunca instalado | Mapster (MIT) o mapeo manual con métodos estáticos |
| **MassTransit** | Apache 2.0 → comercial (2025) | ⚠️ En revisión | ✅ Nunca instalado | `RabbitMQ.Client` (Apache 2.0) directo, envuelto en `IEventBus` |
| **Redis / StackExchange.Redis** | Redis Source Available (2024) | Licencia no OSI | ✅ Nunca instalado | **Valkey** (Linux Foundation, fork OSS de Redis, Apache 2.0) |
| **Hangfire** | LGPL (Community) / Comercial (Pro) | Siempre dual | ⚠️ Permitida solo edición Community | **Quartz.NET** (Apache 2.0, 100% libre) — preferido |

## Dispatcher CQRS Propio (Reemplazo de MediatR)

Ubicación: `Pos.Application/Common/Dispatching/Dispatcher.cs`  
Interfaces: `ICommand<T>`, `ICommandHandler<T,R>`, `IQuery<T>`, `IQueryHandler<T,R>`, `IDispatcher`  
Registro DI: via reflexión directa sobre el assembly en `DependencyInjection.cs`  
Sin dependencias externas. Licencia: propietaria del proyecto.

## Valkey como sustituto de Redis

Cuando se requiera caché distribuida, el paquete a instalar es `Valkey.StackExchange` o cualquier cliente compatible con Valkey (protocolo RESP compatible con Redis). **No instalar `StackExchange.Redis`** con conexión a un servidor Redis comercial — usar Valkey OSS en su lugar. Ver ADR 0011 para las reglas de claves tenant-aware.

## Consecuencias

- **Positivas:** Dependencias auditables, sin deuda oculta de licenciamiento, arquitectura libre de vendor lock-in.
- **Negativas:** El dispatcher propio requiere mantenimiento propio; Valkey puede tener menor ecosistema de documentación que Redis en el corto plazo.
