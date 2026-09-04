# ADR 0013: Política de Licenciamiento de Dependencias — Solo Software Libre

- **Estado:** Aceptado
- **Fecha:** 2026-09-03
- **Contexto:** Múltiples librerías de la comunidad .NET han cambiado de licencia open-source a licencia comercial en los últimos años (MediatR v13 — julio 2025, Redis — 2024, MassTransit — 2025, AutoMapper v13), creando riesgo de costos ocultos o bloqueo de vendor en producción. Se necesita una política explícita para prevenir que dependencias comerciales entren a la solución sin una decisión consciente.

## Decisión

**Solo se permite incorporar paquetes NuGet que cumplan SIMULTÁNEAMENTE:**
1. Licencia libre y perpetuamente gratuita: MIT, Apache 2.0, LGPL, BSD, o equivalente verificado en [choosealicense.com](https://choosealicense.com).
2. La licencia NO contiene cláusulas de "Commons Clause", "BSL (Business Source License)", ni ninguna restricción comercial.
3. Si la librería tiene ediciones "Community" vs. "Pro/Enterprise", solo la edición completamente gratuita está permitida — y únicamente si las características necesarias están cubiertas por la edición gratuita.

## Consecuencias

- **Positivas:** Costo de licenciamiento predecible (cero), sin riesgo de bloqueo de vendor, sin sorpresas legales en auditorías de clientes SaaS.
- **Negativas:** Puede requerir implementación propia o búsqueda de alternativas cuando una librería popular cambia de licencia (ej. MediatR → dispatcher propio, Redis → Valkey).
- **Proceso:** Cada PR que agregue un paquete NuGet nuevo debe incluir verificación explícita de licencia en la descripción del PR.
