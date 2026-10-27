using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pos.Api.Options;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Seed;

/// <summary>
/// Proceso de infraestructura para la siembra e hiper-idempotencia del primer SuperAdmin del sistema.
/// </summary>
public sealed partial class SuperAdminSeeder
{
    private readonly PosDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly SuperAdminSettings _settings;
    private readonly ILogger<SuperAdminSeeder> _logger;
    private readonly TimeProvider _timeProvider;

    public SuperAdminSeeder(
        PosDbContext context,
        IPasswordHasher passwordHasher,
        IOptions<SuperAdminSettings> settings,
        ILogger<SuperAdminSeeder> logger,
        TimeProvider timeProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        string? email = _settings.Email;
        string? rawPassword = _settings.Password;

        // Crear o verificar el Rol SuperAdmin global con permisos de plataforma
        var superAdminRole = await _context.Set<Role>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == Role.SuperAdminRoleId, cancellationToken);
            
        if (superAdminRole == null)
        {
            superAdminRole = Role.CreatePlatformRole(
                Role.SuperAdminRoleId,
                PlatformScope.TenantId,
                PlatformRoles.SuperAdmin,
                "Administrador Global del Sistema",
                new[] { Permissions.Platform.DeadLetterView, Permissions.Platform.DeadLetterReprocess },
                _timeProvider);
            await _context.Set<Role>().AddAsync(superAdminRole, cancellationToken);
        }
        else
        {
            superAdminRole.AddPermission(Permissions.Platform.DeadLetterView);
            superAdminRole.AddPermission(Permissions.Platform.DeadLetterReprocess);
        }

        var existingSuperAdmin = await _context.Users
            .FirstOrDefaultAsync(u => u.RoleId == Role.SuperAdminRoleId, cancellationToken);

        if (existingSuperAdmin != null)
        {
            if (!string.IsNullOrWhiteSpace(rawPassword))
            {
                // Opción B (Sincronización): Verifica si el secreto rotó.
                // Solo actualiza la BD si la contraseña en user-secrets ya no coincide con el hash existente.
                bool matchesCurrentSecret = _passwordHasher.Verify(rawPassword, existingSuperAdmin.PasswordHash);
                if (!matchesCurrentSecret)
                {
                    string newHash = _passwordHasher.HashPassword(rawPassword);
                    existingSuperAdmin.UpdatePassword(new PasswordHash(newHash), _timeProvider);
                    await _context.SaveChangesAsync(cancellationToken);
                    LogSuperAdminPasswordUpdated(_logger, existingSuperAdmin.Email.Value);
                    return;
                }
            }
            await _context.SaveChangesAsync(cancellationToken);
            LogSuperAdminAlreadyExists(_logger);
            return;
        }

        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException(
                "[SuperAdminSeeder] La clave 'SuperAdminSettings:Email' no esta configurada.");

        if (string.IsNullOrWhiteSpace(rawPassword))
            throw new InvalidOperationException(
                "[SuperAdminSeeder] La clave 'SuperAdminSettings:Password' no esta configurada.");

        string firstName = string.IsNullOrWhiteSpace(_settings.FirstName) ? "Super" : _settings.FirstName;
        string lastName  = string.IsNullOrWhiteSpace(_settings.LastName) ? "Admin" : _settings.LastName;

        string passwordHash = _passwordHasher.HashPassword(rawPassword);

        var superAdmin = User.Create(
            email:        email,
            passwordHash: passwordHash,
            roleId:       Role.SuperAdminRoleId,
            tenantId:     null,
            firstName:    firstName,
            lastName:     lastName,
            timeProvider: _timeProvider);

        await _context.Users.AddAsync(superAdmin, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        LogSuperAdminSeeded(_logger, email);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SuperAdminSeeder: SuperAdmin ya existe. Siembra omitida.")]
    private static partial void LogSuperAdminAlreadyExists(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SuperAdminSeeder: Contraseña del SuperAdmin actualizada desde configuración. Email={Email}")]
    private static partial void LogSuperAdminPasswordUpdated(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SuperAdminSeeder: SuperAdmin sembrado exitosamente. Email={Email}")]
    private static partial void LogSuperAdminSeeded(ILogger logger, string email);
}
