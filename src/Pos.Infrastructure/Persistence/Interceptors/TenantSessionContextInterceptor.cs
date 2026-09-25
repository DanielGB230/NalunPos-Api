using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pos.Application.Common.Interfaces;

namespace Pos.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Interceptor de EF Core para Row-Level Security (RLS) en SQL Server.
/// Inyecta el TenantId y el indicador IsSuperAdmin en el SESSION_CONTEXT de la conexión ADO.NET
/// al abrir la conexión, previniendo fuga de datos cruzados en el Connection Pooling.
/// </summary>
public class TenantSessionContextInterceptor : DbConnectionInterceptor
{
    private readonly ICurrentTenantContext _tenantContext;

    public TenantSessionContextInterceptor(ICurrentTenantContext tenantContext)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetSessionContext(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await SetSessionContextAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private void SetSessionContext(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            EXEC sys.sp_set_session_context @key = N'TenantId', @value = @tenantId;
            EXEC sys.sp_set_session_context @key = N'IsSuperAdmin', @value = @isSuperAdmin;";

        var tenantIdParam = command.CreateParameter();
        tenantIdParam.ParameterName = "@tenantId";
        tenantIdParam.Value = _tenantContext.TenantId.HasValue ? (object)_tenantContext.TenantId.Value : DBNull.Value;
        command.Parameters.Add(tenantIdParam);

        var isSuperAdminParam = command.CreateParameter();
        isSuperAdminParam.ParameterName = "@isSuperAdmin";
        isSuperAdminParam.Value = _tenantContext.IsSuperAdmin ? (object)1 : (object)0;
        command.Parameters.Add(isSuperAdminParam);

        command.ExecuteNonQuery();
    }

    private async Task SetSessionContextAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            EXEC sys.sp_set_session_context @key = N'TenantId', @value = @tenantId;
            EXEC sys.sp_set_session_context @key = N'IsSuperAdmin', @value = @isSuperAdmin;";

        var tenantIdParam = command.CreateParameter();
        tenantIdParam.ParameterName = "@tenantId";
        tenantIdParam.Value = _tenantContext.TenantId.HasValue ? (object)_tenantContext.TenantId.Value : DBNull.Value;
        command.Parameters.Add(tenantIdParam);

        var isSuperAdminParam = command.CreateParameter();
        isSuperAdminParam.ParameterName = "@isSuperAdmin";
        isSuperAdminParam.Value = _tenantContext.IsSuperAdmin ? (object)1 : (object)0;
        command.Parameters.Add(isSuperAdminParam);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
