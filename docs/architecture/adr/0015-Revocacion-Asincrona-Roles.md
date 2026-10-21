# ADR 0015: Revocación Asíncrona de Roles y Caché de Permisos

## Contexto

En un sistema multi-tenant, la autorización en cada petición (ICommand/IQuery) es crítica para la seguridad. Sin embargo, consultar los permisos de un rol (a través de la base de datos) en cada petición introduce un acoplamiento fuerte a la base de datos y un impacto significativo en la latencia, afectando la escalabilidad del sistema. 

El modelo de control de acceso basado en roles (RBAC) almacena los permisos asociados a un `RoleId`. Con la refactorización a `RoleId` (removiendo los Enums de roles), es necesario consultar la entidad `Role` para conocer la lista de permisos de un usuario al autorizar una petición. 

## Decisión

Hemos decidido implementar un **modelo de revocación asíncrona de roles** y utilizar un caché en memoria/distribuido mediante **`HybridCache` (novedad de .NET 9)** para los permisos asociados a los roles:

1. **Caché Híbrido (`HybridCache`)**: Almacenaremos los permisos de cada rol (agrupados por `TenantId` y `RoleId`) usando la nueva API `HybridCache` de `Microsoft.Extensions.Caching.Hybrid`.
2. **Time-To-Live (TTL)**: Estableceremos un TTL máximo de **60 segundos** para el caché de permisos.
3. **Propagación Asíncrona**: Cuando un administrador cambie los permisos de un rol o asigne un nuevo rol a un usuario, habrá un retardo de propagación máximo de 60 segundos antes de que la caché expire y el sistema consulte la base de datos para obtener los nuevos permisos.
4. **Validación en Caché**: La interfaz `ICurrentUserPermissions` servirá como puerto, y su implementación `CurrentUserPermissions` en la capa de Infraestructura abstraerá el uso de `HybridCache`.

## Consecuencias

### Positivas
- **Alta Disponibilidad y Menor Latencia**: La base de datos no es bombardeada por consultas para obtener los permisos de los roles en cada petición HTTP.
- **Escalabilidad**: El sistema escala de forma natural, ya que la validación de permisos requiere principalmente lectura de memoria. `HybridCache` se encargará eficientemente de lidiar con problemas como *cache stampede*.
- **Desacoplamiento**: La capa de Aplicación solo depende de `ICurrentUserPermissions`, ignorando si los datos provienen de base de datos o memoria.

### Negativas (y Mitigaciones)
- **Lag de Revocación de 60s**: Existe un riesgo de diseño: si a un cajero se le revoca un permiso malicioso (ej. hacer reembolsos), este podría teóricamente seguir usando la acción durante 60 segundos si su caché no ha expirado. 
  - *Justificación (Business Rule)*: Este riesgo de 60 segundos ha sido **aceptado por diseño** para priorizar la latencia del sistema POS. Es un compromiso razonable frente a impactar el 100% de las peticiones.
- **Fail-Closed en Memoria**: Si hay fallos en la obtención, la caché devolverá listas vacías (Fail-Closed) bloqueando el acceso hasta que se resuelva, lo cual es más seguro que permitir peticiones dudosas.
