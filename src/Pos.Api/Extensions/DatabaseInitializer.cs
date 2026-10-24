using Microsoft.EntityFrameworkCore;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Seed;

namespace Pos.Api.Extensions;

/// <summary>
/// Inicializador determinista y fail-fast de base de datos y siembra en el arranque de la API.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));
        var env = services.GetRequiredService<IHostEnvironment>();
        var dbContext = services.GetRequiredService<PosDbContext>();

        // Solo ejecutar migraciones automáticas en entorno Development.
        // En Producción y Staging, las migraciones se ejecutan de manera aislada y pre-despliegue
        // mediante 'dotnet ef migrations bundle' en el pipeline CI/CD (ADR 0019), nunca de forma concurrente
        // desde réplicas de la aplicación.
        if (env.IsDevelopment())
        {
            logger.LogInformation("Aplicando migraciones relacionales pendientes en entorno Development...");
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        // Siembra determinista del SuperAdmin inicial.
        // Fail-Fast: sin try/catch silencioso; si la siembra falla, el host aborta el inicio inmediatamente.
        var seeder = services.GetRequiredService<SuperAdminSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }
}
