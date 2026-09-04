# ADR 0006: Disparadores Objetivos para Migración a Assemblies Físicos por Módulo

- **Estado:** Aprobado
- **Fecha:** 2026-08-26
- **Contexto:** En etapas iniciales de un sistema Monolítico Modular, crear proyectos física y tecnológicamente independientes por cada módulo (ej. `Pos.Modules.Sales.Application`, `Pos.Modules.Inventory.Application`) genera sobrecarga administrativa de compilación, referencias complejas y lentitud en refactorizaciones tempranas sin otorgar beneficios inmediatos.

## Decisión

1. Mantener `Pos.Application` y `Pos.Infrastructure` como ensamblados únicos (single assembly) organizados internamente en carpetas funcionales por módulo (*vertical slicing*).
2. Definir **disparadores objetivos cuantitativos** que obligarán a migrar a proyectos físicos independientes por módulo únicamente cuando se cumpla al menos uno de los siguientes criterios:
   - Un módulo cuenta con un equipo dedicado y sus cambios provocan conflictos de *merge* recurrentes en Git.
   - El tiempo de compilación (*build time*) completo de la solución penaliza el ciclo de desarrollo continuo.
   - Se detectan importaciones directas entre módulos que eluden la comunicación por `IntegrationEvents`.
   - Se toma la decisión formal de extraer un módulo a un microservicio independiente en la red.

## Consecuencias

- **Positivas:** Estructura liviana, rápida velocidad de iteración inicial, refactorizaciones sencillas entre módulos.
- **Negativas:** La disciplina para no importar tipos privados de otros módulos recae en pruebas de arquitectura y revisiones de código.
