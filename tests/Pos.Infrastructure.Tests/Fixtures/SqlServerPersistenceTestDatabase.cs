using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pos.Infrastructure.Persistence.Context;
using Xunit;

namespace Pos.Infrastructure.Tests.Fixtures;

/// <summary>
/// Base de datos SQL Server real para las pruebas de persistencia de Infrastructure.
///
/// Motivo: el comportamiento de <c>datetime2</c> (Kind=Unspecified al leer) solo se
/// reproduce contra SQL Server; los proveedores InMemory no lo emulan.
///
/// Resolución de la connection string (mismo contrato que el pipeline de CI):
///   1. Variable de entorno <c>IntegrationTests__ConnectionString</c>.
///   2. Fallback local a LocalDB con una base cuyo nombre termina en "Tests".
///
/// Fail-closed: se rechaza cualquier base cuyo nombre no termine en "Tests" para
/// impedir destruir bases de desarrollo o producción.
/// </summary>
public sealed class SqlServerPersistenceTestDatabase : IAsyncLifetime
{
    private const string DefaultConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=NalunPos_InfrastructureTests;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;";

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        string configured = Environment.GetEnvironmentVariable("IntegrationTests__ConnectionString") ?? DefaultConnectionString;
        ValidateDatabaseName(configured);
        ConnectionString = configured;

        await using PosDbContext context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public PosDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new PosDbContext(options);
    }

    public static void ValidateDatabaseName(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog) ||
            !builder.InitialCatalog.EndsWith("Tests", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Operación abortada por seguridad: la base de datos de pruebas '{builder.InitialCatalog}' debe terminar en 'Tests'.");
        }
    }
}
