# ADR 0012: Modelo de Onboarding de Tenants (Alta Asistida vs. Self-Service)

- **Estado:** Aceptado
- **Fecha:** 2026-09-03
- **Contexto:** Definir cómo ingresan nuevos tenants (empresas) al sistema SaaS, y qué interfaz está autorizada a iniciar el aprovisionamiento de un tenant nuevo.

## Decisión

**Alta asistida por SuperAdmin** como modelo inicial de onboarding, no catálogo público self-service.

La única interfaz autorizada para disparar el aprovisionamiento de un tenant nuevo (`CreateTenantCommand` en `Application/Platform/Tenants`) es el panel de SuperAdmin (`Api/Controllers/Platform`). No se construye endpoint público de auto-registro en esta fase.

**Diseño agnóstico al origen:** el caso de uso de aprovisionamiento se diseña de forma independiente a quién lo dispara — un SuperAdmin hoy, potencialmente un formulario público mañana. Esto garantiza que la decisión de negocio (cómo entra un nuevo cliente) no contamine el diseño del dominio.

## Justificación

Para un volumen inicial de 3-10 empresas, un POS requiere configuración asistida que un flujo 100% automático no resuelve bien:

- **Facturación electrónica:** requiere configuración específica por país/régimen.
- **Hardware POS:** impresoras, cajas, lectores de código de barras requieren setup asistido.
- **Migración de inventario inicial:** importación de catálogos existentes del cliente.

El costo de construir un catálogo self-service completo (pasarela de pago recurrente, gestión de tarjetas rechazadas, cancelaciones, recuperación de cuentas) no se justifica financieramente a ese volumen.

## Disparadores para construir el Self-Service

Evaluar el catálogo self-service cuando ocurra **cualquiera** de los siguientes:

1. El volumen de altas nuevas por mes empieza a consumir tiempo operativo notable del equipo.
2. Aparecen clientes de perfil "bajo contacto" (un solo local, sin integraciones complejas) para quienes el proceso asistido genera fricción innecesaria.
3. La competencia directa ofrece alta inmediata sin fricción como diferenciador de ventas relevante.

Mientras ninguno de estos disparadores ocurra, la alta asistida es la decisión correcta y no debe revisarse "por si acaso".

## Consecuencias

- **Positivas:** Bajo costo inicial, acompañamiento de calidad al cliente, sin riesgo de cuentas huérfanas mal configuradas.
- **Negativas:** No escala automáticamente; requiere disponibilidad del equipo para cada alta nueva.
- **Neutras:** El comando de aprovisionamiento puede reutilizarse para el self-service futuro sin rediseño.
