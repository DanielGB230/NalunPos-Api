# ADR 0001: Clean Architecture y Reglas de Dependencia Verificables

- **Estado:** Aprobado
- **Fecha:** 2026-08-26
- **Contexto:** Se requiere un backend POS diseñado para operar durante 20+ años. En proyectos de larga duración, la falta de aislamiento tecnológico provoca que el código de negocio quede acoplado a frameworks ORM, frameworks web o SDKs externos, haciendo las migraciones y evoluciones extremadamente costosas o imposibles.

## Decisión

1. Adoptar **Clean Architecture** dividida en 4 capas estrictas: `Pos.Domain`, `Pos.Application`, `Pos.Infrastructure`, y `Pos.Api`.
2. `Pos.Domain` es el centro absoluto y no posee ninguna dependencia técnica externa (sin EF Core, ASP.NET Core, ni clientes HTTP/SDKs).
3. Las reglas de dependencia se validan de forma **automatizada en cada build** mediante el proyecto `Pos.Architecture.Tests` utilizando `TngTech.ArchUnitNET` (en lugar de revisiones manuales proensambladas).

## Consecuencias

- **Positivas:** El núcleo de dominio es inmune a cambios tecnológicos, fácil de testear sin mocks complejos de base de datos, y mantenible por décadas.
- **Negativas:** Requiere mapeo de objetos entre capas (DTOs, Entidades de Dominio, Modelos de Persistencia) y mayor disciplina inicial.
