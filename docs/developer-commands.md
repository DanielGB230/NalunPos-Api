# Guía de Comandos CLI para Desarrollo — Nalun POS Backend

Esta guía contiene la referencia rápida de los comandos oficiales de la CLI de .NET 10 y Entity Framework Core para la administración, compilación, ejecución y seguridad del backend de **Nalun POS**.

> **Nota importante de ubicación**: Todos estos comandos deben ejecutarse desde la raíz del proyecto backend (`D:\NalunPosApp\NalunPos-Api`).

---

## 1. Gestión de Base de Datos (Entity Framework Core)

Los comandos de base de datos utilizan siempre el flag `--project` apuntando a la capa de Infraestructura y `--startup-project` apuntando a la API de arranque.

### Crear una Nueva Migración (Generada automáticamente por EF Core en `src/Pos.Infrastructure/Migrations`)
```bash
dotnet ef migrations add <NombreDeLaMigracion> --project src/Pos.Infrastructure --startup-project src/Pos.Api
```

### Aplicar Migraciones y Actualizar la Base de Datos
```bash
dotnet ef database update --project src/Pos.Infrastructure --startup-project src/Pos.Api
```

### Revertir / Eliminar la Última Migración (No Aplicada)
```bash
dotnet ef migrations remove --project src/Pos.Infrastructure --startup-project src/Pos.Api
```

### Eliminar (Drop) la Base de Datos Completa
```bash
dotnet ef database drop --force --project src/Pos.Infrastructure --startup-project src/Pos.Api
```

### Listar las Migraciones Existentes
```bash
dotnet ef migrations list --project src/Pos.Infrastructure --startup-project src/Pos.Api
```

### Restaurar Herramientas Locales (.NET Tools fijadas en `.config/dotnet-tools.json`)
```bash
dotnet tool restore
```

### Validar que no existan Cambios Pendientes en el Modelo de EF Core
```bash
dotnet ef migrations has-pending-model-changes --project src/Pos.Infrastructure --startup-project src/Pos.Api
```

### Generar Bundle de Migraciones para Producción / CI (ADR 0019)
```bash
# En Windows (genera efbundle.exe)
dotnet ef migrations bundle --project src/Pos.Infrastructure --startup-project src/Pos.Api --output ./efbundle.exe --configuration Release

# En Linux / CI (genera efbundle)
dotnet ef migrations bundle --project src/Pos.Infrastructure --startup-project src/Pos.Api --output ./efbundle --configuration Release
```

### Ejecutar Bundle de Migraciones contra la Base de Datos
```bash
./efbundle --connection "Server=localhost,1433;Database=PosIntegrationTests;User Id=sa;Password=Your_password123!;TrustServerCertificate=True;"
```

---

## 2. Gestión de Seguridad (User Secrets)

Los secretos locales de desarrollo permiten almacenar cadenas de conexión y claves JWT sin exponer datos sensibles en el control de versiones (Git).

### Listar Todos los Secretos Guardados
```bash
dotnet user-secrets list --project src/Pos.Api
```

### Agregar o Modificar un Secreto (ej. Cadena de Conexión)
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\mssqllocaldb;Database=NalunPosDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;" --project src/Pos.Api
```

### Eliminar un Secreto Específico
```bash
dotnet user-secrets remove "ConnectionStrings:DefaultConnection" --project src/Pos.Api
```

### Inicializar User Secrets (Solo en caso de configurar un entorno nuevo)
```bash
dotnet user-secrets init --project src/Pos.Api
```

---

## 3. Compilación, Pruebas y Ejecución

### Limpiar los Binarios de la Solución
```bash
dotnet clean
```

### Compilación Estricta (TreatWarningsAsErrors=true)
```bash
dotnet build
```

### Ejecutar Todas las Pruebas (Unitarias, Arquitectura e Integración)
```bash
dotnet test
```

### Ejecutar Pruebas de Integración con SQL Server
Las pruebas de integración (`Pos.IntegrationTests`) se conectan directamente a la instancia de SQL Server configurada mediante la variable de entorno exclusiva `IntegrationTests__ConnectionString` o `tests/Pos.IntegrationTests/appsettings.IntegrationTests.json`, sin depender de Docker ni Testcontainers.
> **Regla de seguridad Fail-Closed**: El nombre de la base de datos DEBE terminar obligatoriamente en `Tests` (ej. `PosIntegrationTests`). Si no termina en `Tests`, la ejecución abortará inmediatamente para prevenir el borrado accidental (`EnsureDeletedAsync`) de bases de desarrollo o producción.

```bash
# Configurar la variable exclusiva de pruebas de integración si difiere del valor por defecto
$env:IntegrationTests__ConnectionString="Server=localhost,1433;Database=PosIntegrationTests;User Id=sa;Password=Your_password123!;TrustServerCertificate=True;"
dotnet test tests/Pos.IntegrationTests
```

### Ejecutar la API Forzando el Perfil HTTPS
```bash
dotnet run --project src/Pos.Api --launch-profile "https"
```
