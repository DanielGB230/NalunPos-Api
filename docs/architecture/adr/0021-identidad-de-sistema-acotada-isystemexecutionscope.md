# ADR 0021: Identidad de Sistema Acotada y Eliminación del Bypass por `HttpContext == null`

- **Estado:** Aceptado
- **Fecha:** 2026-10-22
- **Contexto:** Garantizar la integridad del modelo de autorización Fail-Closed en toda la aplicación, erradicando el bypass tácito donde cualquier contexto fuera de HTTP (`HttpContext == null`) saltaba los pipeline behaviors de autorización (`AuthorizationBehavior` y `AuthorizationQueryBehavior`).

---

## 1. Problema Previo

- En la implementación anterior, `CurrentUserService.IsSystemContext` se evaluaba como `_httpContextAccessor.HttpContext == null`.
- En `AuthorizationBehavior` y `AuthorizationQueryBehavior`, la condición:
  ```csharp
  if (_currentUserService.IsSystemContext)
  {
      return await _inner.HandleAsync(request, cancellationToken);
  }
  ```
  otorgaba autorización irrestricta a cualquier ejecución invocada fuera de una petición HTTP (workers en segundo plano, pruebas, interceptores o comandos invocados por error).
- Como consecuencia, los atributos `[HasPermission]` y la política Fail-Closed quedaban decorativos e inoperantes ante cualquier llamada fuera del contexto web tradicional.

---

## 2. Decisión Arquitectónica

### A. Contrato `ISystemExecutionScope`
Se introduce la interfaz de aplicación `ISystemExecutionScope` (`Pos.Application.Common.Interfaces`):
```csharp
public interface ISystemExecutionScope
{
    bool IsActive { get; }
    string? WorkerName { get; }
    bool IsCommandAllowed(Type requestType);
    IDisposable BeginScope(string workerName, IEnumerable<Type> allowedRequestTypes);
}
```

### B. Implementación Scoped en Infraestructura
`SystemExecutionScope` (`Pos.Infrastructure.Multitenancy`) almacena de forma aislada por scope el nombre del worker y el conjunto estricto de tipos de comandos/queries autorizados (`HashSet<Type>`). Al destruirse el objeto disposable (`Dispose`), el ámbito de sistema se resetea inmediatamente.

### C. Autorización Fail-Closed en Pipeline Behaviors
Se reemplaza la comprobación de `_currentUserService.IsSystemContext` en `AuthorizationBehavior` y `AuthorizationQueryBehavior`:
```csharp
if (_systemExecutionScope.IsActive)
{
    if (_systemExecutionScope.IsCommandAllowed(requestType))
    {
        return await _inner.HandleAsync(request, cancellationToken);
    }

    throw new ForbiddenDomainException(
        $"El contexto de sistema '{_systemExecutionScope.WorkerName}' no tiene autorización para ejecutar '{requestType.Name}' (Fail-Closed).");
}
```
Si un proceso en segundo plano intenta despachar un comando no declarado en su lista de permitidos, la ejecución falla inmediatamente con `ForbiddenDomainException`. Si no hay ningún `ISystemExecutionScope` activo, se exige autenticación y permisos de usuario, bloqueando cualquier bypass no autorizado.

### D. Activación Declarativa en Background Workers
Únicamente los workers legítimos de la plataforma activan un ámbito acotado antes de despachar comandos. Por ejemplo:
```csharp
using var executionScope = systemScope.BeginScope(
    "InvoiceReconciliationBackgroundService",
    new[] { typeof(ReconcileInvoiceCommand) });
```

### E. Guarda Roslyn de Arquitectura
En `Pos.Architecture.Tests`, el test `SystemExecutionScope_BeginScope_Must_Only_Be_Invoked_By_Authorized_BackgroundWorkers` utiliza Roslyn Syntax Trees para validar estáticamente que `BeginScope` solo sea invocado dentro de los archivos de background workers explícitamente autorizados (`InvoiceReconciliationBackgroundService.cs` y `OutboxProcessorBackgroundService.cs`), previniendo la proliferación de scopes de sistema no auditados.

---

## 3. Consecuencias

- **Positivas:**
  - `[HasPermission]` y las políticas Fail-Closed se aplican de manera uniforme y determinista.
  - Principio de mínimo privilegio: los procesos en segundo plano solo pueden ejecutar exactamente los comandos que tienen asignados.
  - Gobernanza estricta protegida por análisis estático Roslyn en CI.
- **Negativas:**
  - Nuevos workers o procesos de fondo requerirán declarar sus tipos permitidos y ser incluidos en la lista autorizada del test de arquitectura.
