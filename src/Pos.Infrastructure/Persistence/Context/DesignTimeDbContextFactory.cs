using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Pos.Infrastructure.Persistence.Context;

/// <summary>
/// Fábrica de DbContext a tiempo de diseño para la CLI de EF Core
/// (<c>dotnet ef migrations add</c> / <c>dotnet ef database update</c>).
///
/// Principios de seguridad aplicados:
///   - NUNCA almacena ni tiene fallback con credenciales de base de datos.
///   - La connection string se lee exclusivamente desde:
///       1. dotnet user-secrets del proyecto Pos.Api (desarrollo local).
///       2. Variable de entorno <c>EFCORE_CONNECTIONSTRING</c> (CI/CD, pipelines).
///   - Si ninguna fuente provee un valor válido, falla explícitamente con un
///     mensaje de error accionable en lugar de silenciar el problema.
///
/// Integración CI/CD recomendada:
///   Exportar antes del comando ef: <c>$env:EFCORE_CONNECTIONSTRING = "..."</c>
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PosDbContext>
{
    private const string EnvVarName = "EFCORE_CONNECTIONSTRING";


    public PosDbContext CreateDbContext(string[] args)
    {
        string apiDirectory = ResolveApiProjectDirectory();

        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets(typeof(DesignTimeDbContextFactory).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();



        // Prioridad 1: ConnectionStrings:DefaultConnection (user-secrets / appsettings)
        string? connectionString = configuration.GetConnectionString("DefaultConnection");

        // Prioridad 2: Variable de entorno dedicada para CLI/CI (evita depender del appsettings)
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = Environment.GetEnvironmentVariable(EnvVarName);

        // Sin connection string válida → fallo explícito y accionable (nunca silencioso)
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"""
                [DesignTimeDbContextFactory] No se encontró una connection string válida.

                Para desarrollo local, configura el user-secret:
                  dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=...;Database=NalunPosDb;..."
                  (ejecutar desde el directorio de Pos.Api, secrets id: f8cd60cc-c652-42cf-8ad9-07514cac47b3)


                Para CI/CD, exporta la variable de entorno:
                  $env:{EnvVarName} = "Server=...;Database=NalunPosDb;..."
                """);
        }

        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlServer(connectionString,
            sql => sql.MigrationsAssembly(typeof(PosDbContext).Assembly.FullName));

        // Design-time: sin tenant activo (null), sin interceptores de request
        return new PosDbContext(optionsBuilder.Options, null, null, null);
    }

    /// <summary>
    /// Resuelve la ruta del directorio del proyecto Pos.Api desde cualquier
    /// directorio de trabajo posible (raíz de solución, Infrastructure, Api).
    /// </summary>
    private static string ResolveApiProjectDirectory()
    {
        string currentDir = Directory.GetCurrentDirectory();

        // Caso 1: ejecutado desde Pos.Api directamente
        if (File.Exists(Path.Combine(currentDir, "Pos.Api.csproj")))
            return currentDir;

        // Caso 2: ejecutado desde la raíz de la solución (src/Pos.Api)
        string fromRoot = Path.Combine(currentDir, "src", "Pos.Api");
        if (Directory.Exists(fromRoot))
            return fromRoot;

        // Caso 3: ejecutado desde Pos.Infrastructure (../Pos.Api)
        string fromInfra = Path.GetFullPath(Path.Combine(currentDir, "..", "Pos.Api"));
        if (Directory.Exists(fromInfra))
            return fromInfra;

        // Caso 4: ejecutado desde la raíz con estructura src/*/Pos.Api
        var discovered = Directory.GetFiles(currentDir, "Pos.Api.csproj", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (discovered is not null)
            return Path.GetDirectoryName(discovered)!;

        throw new DirectoryNotFoundException(
            $"No se pudo localizar el directorio del proyecto Pos.Api desde: '{currentDir}'. " +
            "Ejecuta el comando ef desde la raíz de la solución o desde el proyecto Pos.Api.");
    }
}
