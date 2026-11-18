using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Tests.Fixtures;
using Xunit;

namespace Pos.Infrastructure.Tests;

/// <summary>
/// Contrato de tiempo en persistencia: un instante escrito con Kind=Utc debe leerse
/// de SQL Server con Kind=Utc. SQL Server <c>datetime2</c> no almacena Kind, por lo que
/// sin conversión el valor vuelve como Kind=Unspecified y un cliente lo interpreta como
/// hora local.
/// </summary>
public class UtcDateTimePersistenceTests : IClassFixture<SqlServerPersistenceTestDatabase>
{
    private readonly SqlServerPersistenceTestDatabase _database;

    public UtcDateTimePersistenceTests(SqlServerPersistenceTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task TenantCreatedAtUtc_ReadFromSqlServer_HasKindUtc()
    {
        // Arrange
        Tenant tenant = Tenant.Create(
            $"UtcKind_{Guid.NewGuid():N}",
            Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            TimeProvider.System);

        await using (PosDbContext writeContext = _database.CreateContext())
        {
            writeContext.Tenants.Add(tenant);
            await writeContext.SaveChangesAsync();
        }

        // Act
        await using PosDbContext readContext = _database.CreateContext();
        Tenant reloaded = await readContext.Tenants
            .AsNoTracking()
            .SingleAsync(t => t.Id == tenant.Id);

        // Assert
        Assert.Equal(DateTimeKind.Utc, reloaded.CreatedAtUtc.Kind);
        Assert.Equal(tenant.CreatedAtUtc, reloaded.CreatedAtUtc);
    }
}
