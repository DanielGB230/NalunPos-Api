using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Authentication;

/// <summary>
/// Servicio de infraestructura que implementa <see cref="IAuthUserLookup"/>.
/// Ejecuta consultas explícitas con IgnoreQueryFilters() dentro de un
/// <see cref="IAuthenticationSessionScope"/> para permitir la autenticación de usuarios
/// cuando aún no hay un contexto de tenant resuelto en la petición HTTP.
/// Ver: ADR 0008 — Estrategia de Aislamiento de Tenant.
/// </summary>
public class AuthUserLookup : IAuthUserLookup
{
    private readonly PosDbContext _dbContext;
    private readonly IAuthenticationSessionScope _sessionScope;

    public AuthUserLookup(PosDbContext dbContext, IAuthenticationSessionScope sessionScope)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _sessionScope = sessionScope ?? throw new ArgumentNullException(nameof(sessionScope));
    }

    public async Task<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        string normalized = email.Trim().ToLowerInvariant();
        var emailVo = new Email(normalized);

        await using var session = await _sessionScope.BeginAsync(cancellationToken);

        User? user;
        await session.EnableGlobalUserLookupAsync(cancellationToken);
        try
        {
            user = await _dbContext.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == emailVo, cancellationToken);
        }
        finally
        {
            await session.DisableGlobalUserLookupAsync(cancellationToken);
        }

        if (user is null)
        {
            return null;
        }

        // 1. Caso SuperAdmin: RoleId debe ser SuperAdminRoleId Y TenantId debe ser null (fail-closed)
        if (user.RoleId == Role.SuperAdminRoleId && !user.TenantId.HasValue)
        {
            return new AuthUser(user, PlatformRoles.SuperAdmin);
        }

        if (user.RoleId == Role.SuperAdminRoleId || !user.TenantId.HasValue)
        {
            return null;
        }

        // 2. Caso Usuario de Tenant: acota el scope al TenantId del usuario y resuelve el Role
        await session.SetTenantScopeAsync(user.TenantId.Value, cancellationToken);

        var role = await _dbContext.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == user.RoleId && r.TenantId == user.TenantId.Value, cancellationToken);

        if (role is null ||
            string.IsNullOrWhiteSpace(role.Name) ||
            string.Equals(role.Name.Trim(), PlatformRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new AuthUser(user, role.Name.Trim());
    }

    public async Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        string normalized = email.Trim().ToLowerInvariant();
        var emailVo = new Email(normalized);

        await using var session = await _sessionScope.BeginAsync(cancellationToken);
        await session.EnableGlobalUserLookupAsync(cancellationToken);

        return await _dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == emailVo && (excludeId == null || u.Id != excludeId.Value), cancellationToken);
    }
}
