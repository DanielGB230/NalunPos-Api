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

### Ejecutar Todas las Pruebas Unitarias y de Arquitectura
```bash
dotnet test
```

### Ejecutar la API Forzando el Perfil HTTPS
```bash
dotnet run --project src/Pos.Api --launch-profile "https"
dotnet run --launch-profile "https"

```
