using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Authentication;

/// <summary>
/// Administra de forma segura y balanceada la conexión ADO.NET y las claves de
/// <c>SESSION_CONTEXT</c> en SQL Server durante la autenticación en <see cref="AuthUserLookup"/>.
/// </summary>
public sealed class SqlServerAuthenticationSessionScope : IAuthenticationSessionScope
{
    private const string AllowGlobalUserLookupKey = "AllowGlobalUserLookup";

    private const string SqlReadPreviousContext = """
        SELECT
            TRY_CAST(SESSION_CONTEXT(N'TenantId') AS UNIQUEIDENTIFIER),
            TRY_CAST(SESSION_CONTEXT(N'IsSuperAdmin') AS INT);
        """;

    private const string SqlSetGlobalLookupFlag =
        "EXEC sys.sp_set_session_context @key = N'" + AllowGlobalUserLookupKey + "', @value = @allowGlobalUserLookup;";

    private const string SqlSetFullAuthenticationScope = """
        EXEC sys.sp_set_session_context @key = N'AllowGlobalUserLookup', @value = @allowGlobalUserLookup;
        EXEC sys.sp_set_session_context @key = N'IsSuperAdmin', @value = @isSuperAdmin;
        EXEC sys.sp_set_session_context @key = N'TenantId', @value = @tenantId;
        """;

    private readonly PosDbContext _dbContext;

    public SqlServerAuthenticationSessionScope(PosDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IAuthenticationSession> BeginAsync(CancellationToken cancellationToken = default)
    {
        var connection = _dbContext.Database.GetDbConnection();
        bool wasConnectionClosed = connection.State == ConnectionState.Closed;

        if (wasConnectionClosed)
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            var (previousTenantId, previousIsSuperAdmin) = await ReadPreviousContextAsync(connection, cancellationToken);
            return new SqlServerAuthenticationSession(this, wasConnectionClosed, previousTenantId, previousIsSuperAdmin);
        }
        catch
        {
            if (wasConnectionClosed)
            {
                await _dbContext.Database.CloseConnectionAsync();
            }

            throw;
        }
    }

    internal async Task SetGlobalUserLookupAsync(bool enabled, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        using var cmd = CreateBoundCommand(connection, SqlSetGlobalLookupFlag);
        AddParameter(cmd, "@allowGlobalUserLookup", enabled ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    internal async Task SetTenantScopeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        using var cmd = CreateBoundCommand(connection, SqlSetFullAuthenticationScope);
        AddParameter(cmd, "@allowGlobalUserLookup", 0);
        AddParameter(cmd, "@isSuperAdmin", 0);
        AddParameter(cmd, "@tenantId", tenantId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    internal async Task RestoreAndCloseAsync(
        bool shouldCloseConnection,
        Guid? previousTenantId,
        int? previousIsSuperAdmin)
    {
        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            if (connection.State == ConnectionState.Open)
            {
                using var cmd = CreateBoundCommand(connection, SqlSetFullAuthenticationScope);
                AddParameter(cmd, "@allowGlobalUserLookup", 0);
                AddParameter(cmd, "@isSuperAdmin", previousIsSuperAdmin.HasValue ? previousIsSuperAdmin.Value : DBNull.Value);
                AddParameter(cmd, "@tenantId", previousTenantId.HasValue ? previousTenantId.Value : DBNull.Value);
                await cmd.ExecuteNonQueryAsync(CancellationToken.None);
            }
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await _dbContext.Database.CloseConnectionAsync();
            }
        }
    }

    private async Task<(Guid? TenantId, int? IsSuperAdmin)> ReadPreviousContextAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        using var cmd = CreateBoundCommand(connection, SqlReadPreviousContext);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return (null, null);
        }

        Guid? tenantId = await reader.IsDBNullAsync(0, cancellationToken) ? null : reader.GetGuid(0);
        int? isSuperAdmin = await reader.IsDBNullAsync(1, cancellationToken) ? null : reader.GetInt32(1);
        return (tenantId, isSuperAdmin);
    }

    private DbCommand CreateBoundCommand(DbConnection connection, string commandText)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = commandText;
        cmd.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
        return cmd;
    }

    private static void AddParameter(DbCommand cmd, string name, object value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        cmd.Parameters.Add(param);
    }
}

