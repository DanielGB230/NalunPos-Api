using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Authentication;

/// <summary>
/// Servicio de infraestructura que implementa <see cref="IAuthUserLookup"/>.
/// Ejecuta una consulta explícita con IgnoreQueryFilters() para permitir la autenticación de usuarios
/// (SuperAdmin y Tenant Users) cuando aún no hay un contexto de tenant resuelto en la petición HTTP.
/// Ver: ADR 0008 — Estrategia de Aislamiento de Tenant.
/// </summary>
public class AuthUserLookup : IAuthUserLookup
{
    private readonly PosDbContext _dbContext;

    public AuthUserLookup(PosDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        string normalized = email.Trim().ToLowerInvariant();
        var emailVo = new Pos.Domain.ValueObjects.Email(normalized);

        await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        var connection = _dbContext.Database.GetDbConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'AllowGlobalUserLookup', @value = 1;";
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        try
        {
            return await _dbContext.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == emailVo, cancellationToken);
        }
        finally
        {
            cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'AllowGlobalUserLookup', @value = 0;";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        string normalized = email.Trim().ToLowerInvariant();
        var emailVo = new Pos.Domain.ValueObjects.Email(normalized);

        await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        var connection = _dbContext.Database.GetDbConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'AllowGlobalUserLookup', @value = 1;";
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        try
        {
            return await _dbContext.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Email == emailVo && (excludeId == null || u.Id != excludeId.Value), cancellationToken);
        }
        finally
        {
            cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'AllowGlobalUserLookup', @value = 0;";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
