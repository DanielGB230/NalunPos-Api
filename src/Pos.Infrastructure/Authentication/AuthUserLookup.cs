using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Authentication;

/// <summary>
/// Servicio de infraestructura que implementa <see cref="IAuthUserLookup"/>.
/// Ejecuta una consulta explícita con IgnoreQueryFilters() para permitir la autenticación de usuarios
/// (SuperAdmin y Tenant Users) cuando aún no hay un contexto de tenant resuelto en la petición HTTP.
/// Resuelve de forma segura el nombre del rol dentro del contexto de sesión del tenant.
/// Ver: ADR 0008 — Estrategia de Aislamiento de Tenant.
/// </summary>
public class AuthUserLookup : IAuthUserLookup
{
    private readonly PosDbContext _dbContext;

    public AuthUserLookup(PosDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        string normalized = email.Trim().ToLowerInvariant();
        var emailVo = new Pos.Domain.ValueObjects.Email(normalized);

        DbCommand? cmd = null;
        try
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
            var connection = _dbContext.Database.GetDbConnection();
            cmd = connection.CreateCommand();
            cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'AllowGlobalUserLookup', @value = 1;";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Proveedor no relacional (ej. InMemory en pruebas de integración de API) no soporta DbConnection ni SESSION_CONTEXT.
            cmd = null;
        }

        try
        {
            var user = await _dbContext.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == emailVo, cancellationToken);

            if (user is null)
            {
                return null;
            }

            // 1. Caso SuperAdmin: RoleId es SuperAdminRoleId o TenantId es null
            if (user.RoleId == Role.SuperAdminRoleId || !user.TenantId.HasValue)
            {
                return new AuthUser(user, PlatformRoles.SuperAdmin);
            }

            // 2. Caso Usuario de Tenant: Establece temporalmente el TenantId del usuario en SESSION_CONTEXT
            // para que la política RLS permita leer únicamente el rol perteneciente a dicho tenant.
            if (cmd is not null)
            {
                cmd.Parameters.Clear();
                cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'TenantId', @value = @tenantId;";
                var tenantParam = cmd.CreateParameter();
                tenantParam.ParameterName = "@tenantId";
                tenantParam.Value = user.TenantId.Value;
                cmd.Parameters.Add(tenantParam);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            var role = await _dbContext.Roles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.TenantId == user.TenantId.Value && r.Id == user.RoleId, cancellationToken);

            // Si el rol no existe o su nombre no es válido, falla cerrado (no autentica)
            if (role is null || string.IsNullOrWhiteSpace(role.Name))
            {
                return null;
            }

            return new AuthUser(user, role.Name);
        }
        finally
        {
            if (cmd is not null)
            {
                cmd.Parameters.Clear();
                cmd.CommandText = @"
                    EXEC sys.sp_set_session_context @key = N'AllowGlobalUserLookup', @value = 0;
                    EXEC sys.sp_set_session_context @key = N'TenantId', @value = NULL;";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
                cmd.Dispose();
            }
        }
    }

    public async Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        string normalized = email.Trim().ToLowerInvariant();
        var emailVo = new Pos.Domain.ValueObjects.Email(normalized);

        DbCommand? cmd = null;
        try
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
            var connection = _dbContext.Database.GetDbConnection();
            cmd = connection.CreateCommand();
            cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'AllowGlobalUserLookup', @value = 1;";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            cmd = null;
        }

        try
        {
            return await _dbContext.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Email == emailVo && (excludeId == null || u.Id != excludeId.Value), cancellationToken);
        }
        finally
        {
            if (cmd is not null)
            {
                cmd.Parameters.Clear();
                cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'AllowGlobalUserLookup', @value = 0;";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
                cmd.Dispose();
            }
        }
    }
}
