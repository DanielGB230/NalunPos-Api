# ADR 0007: Gobernanza y Control de Acciones de Agentes Autónomos de IA

- **Estado:** Aprobado
- **Fecha:** 2026-08-26
- **Contexto:** La incorporación futura de Agentes de IA autónomos (sistemas capaces de proponer y ejecutar acciones multipasos como generar órdenes de compra o ajustar inventario) introduce riesgos de seguridad operativa si interactúan directamente con el Dominio sin supervisión o control transaccional.

## Decisión

1. Distinguir formalmente entre **IA Asistencial** (`Pos.Application/AI/Abstractions` — solo consulta y responde) e **IA Agéntica** (`Pos.Application/AI/Agents` — propone y ejecuta acciones).
2. Todo agente que proponga una acción sobre el sistema deberá implementar el contrato `IAgentProposedAction`.
3. Ningún agente escribirá directamente en las entidades de dominio ni en los repositorios sin pasar por los mecanismos de **Gobernanza de Agentes** (`Pos.Infrastructure/AiOrchestration/AgentGovernance`).
4. Aplicar cuatro principios de gobernanza inviolables:
   - **Trazabilidad Obligatoria:** registro completo de auditoría (agente, modelo, timestamp, contexto, justificación).
   - **Idempotencia:** clave de idempotencia única para prevenir ejecuciones repetidas accidentalmente.
   - **Human-in-the-Loop por Nivel de Riesgo:** clasificación previa de acciones en autónomas (ej. alertas de stock) y acciones que exigen confirmación humana explícita (ej. creación de órdenes de compra, cambio de precios).
   - **Reversibilidad y Marca:** identificar toda transacción generada por IA para posibilitar acciones de compensación si fuera necesario.

## Consecuencias

- **Positivas:** Seguridad operativa total contra comportamientos no deseados o alucinaciones de modelos de IA en producción.
- **Negativas:** Obliga a diseñar flujos de gobernanza y UI/UX de aprobación humana antes de desplegar cualquier agente de IA con permisos de escritura.
