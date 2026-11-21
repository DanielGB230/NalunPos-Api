using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pos.Infrastructure.Persistence.Context;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

/// <summary>
/// Verifica la migración de vigencia absoluta de la familia (F1.G.6) sobre una
/// base aislada: las filas existentes reciben el backfill
/// <c>FamilyExpiresAtUtc = ExpiresAtUtc</c>, la columna queda NOT NULL y se crea
/// el índice filtrado de purga.
/// </summary>
[Collection("IntegrationTests")]
public class RefreshTokenFamilyExpiryMigrationTests
{
    private readonly MsSqlTestFixture _fixture;

    public RefreshTokenFamilyExpiryMigrationTests(MsSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Migration_BackfillsExistingRows_AndCreatesNotNullColumnAndFilteredIndex()
    {
        var connectionBuilder = new SqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            InitialCatalog = $"PosFamilyBackfill_{Guid.NewGuid():N}Tests"
        };

        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlServer(connectionBuilder.ConnectionString)
            .Options;

        await using var db = new PosDbContext(options);
        try
        {
            await db.Database.MigrateAsync("20261119170552_AddRefreshTokens");

            Guid rowId = Guid.NewGuid();
            DateTime createdAtUtc = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            DateTime expiresAtUtc = createdAtUtc.AddDays(7);
            string tokenHash = new('a', 64);

            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO RefreshTokens
                    (Id, UserId, TokenHash, FamilyId, ReplacedByTokenHash, RevokedReason, CreatedAtUtc, ExpiresAtUtc, RevokedAtUtc)
                VALUES
                    ({rowId}, {Guid.NewGuid()}, {tokenHash}, {Guid.NewGuid()}, NULL, NULL, {createdAtUtc}, {expiresAtUtc}, NULL)
                """);

            await db.Database.MigrateAsync();

            object? columnLength = await ScalarAsync(
                db,
                "SELECT COL_LENGTH('RefreshTokens', 'FamilyExpiresAtUtc')");

            Assert.False(
                columnLength is null or DBNull,
                "La migración no creó la columna RefreshTokens.FamilyExpiresAtUtc.");

            object? backfilled = await ScalarAsync(
                db,
                $"SELECT FamilyExpiresAtUtc FROM RefreshTokens WHERE Id = '{rowId}'");

            Assert.Equal(expiresAtUtc, Assert.IsType<DateTime>(backfilled));

            object? isNullable = await ScalarAsync(
                db,
                "SELECT is_nullable FROM sys.columns WHERE object_id = OBJECT_ID('RefreshTokens') AND name = 'FamilyExpiresAtUtc'");

            Assert.False(Assert.IsType<bool>(isNullable), "FamilyExpiresAtUtc debe quedar NOT NULL.");

            object? hasFilter = await ScalarAsync(
                db,
                "SELECT has_filter FROM sys.indexes WHERE object_id = OBJECT_ID('RefreshTokens') AND name = 'IX_RefreshTokens_RevokedAtUtc'");

            Assert.True(Assert.IsType<bool>(hasFilter), "El índice IX_RefreshTokens_RevokedAtUtc debe ser filtrado.");

            object? filterDefinition = await ScalarAsync(
                db,
                "SELECT filter_definition FROM sys.indexes WHERE object_id = OBJECT_ID('RefreshTokens') AND name = 'IX_RefreshTokens_RevokedAtUtc'");

            string filter = Assert.IsType<string>(filterDefinition);
            Assert.Contains("RevokedAtUtc", filter, StringComparison.Ordinal);
        }
        finally
        {
            try
            {
                await db.Database.EnsureDeletedAsync();
            }
            catch (SqlException)
            {
                // La base pudo no haberse creado si la migración falló antes;
                // no se enmascara el fallo original.
            }
        }
    }

    private static async Task<object?> ScalarAsync(PosDbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        bool wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed)
        {
            await db.Database.OpenConnectionAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return await command.ExecuteScalarAsync();
        }
        finally
        {
            if (wasClosed)
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }
}
