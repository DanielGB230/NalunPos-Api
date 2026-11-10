using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pos.Application.Common.Authorization;
using Pos.Domain.Entities;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Seed;

public class DefaultRoleSeeder
{
    private readonly PosDbContext _context;
    private readonly ILogger<DefaultRoleSeeder> _logger;
    private readonly TimeProvider _timeProvider;

    public DefaultRoleSeeder(PosDbContext context, ILogger<DefaultRoleSeeder> logger, TimeProvider timeProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task SeedRolesForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        foreach (var definition in DefaultTenantRoleCatalog.All)
        {
            await SeedRoleAsync(tenantId, definition, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRoleAsync(
        Guid tenantId,
        TenantRoleDefinition definition,
        CancellationToken cancellationToken)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == definition.Name, cancellationToken);
        if (role == null)
        {
            role = Role.CreateSystemRole(tenantId, definition.Name, definition.Description, definition.Permissions, _timeProvider);
            _context.Roles.Add(role);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Creado rol {RoleName} para el tenant {TenantId} con {Count} permisos.", definition.Name, tenantId, definition.Permissions.Count);
            }
        }
        else
        {
            // Garantizar idempotencia asegurando que existan los permisos especificados
            foreach (var perm in definition.Permissions)
            {
                if (!role.Permissions.Contains(perm, StringComparer.OrdinalIgnoreCase))
                {
                    role.AddPermission(perm);
                }
            }
        }
    }
}
