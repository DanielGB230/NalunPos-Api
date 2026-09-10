using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pos.Application.Common.Interfaces;
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
    private readonly IConfiguration _configuration;
    private readonly ILogger<SuperAdminSeeder> _logger;

    public SuperAdminSeeder(
        PosDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<SuperAdminSeeder> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        string? email = _configuration["SuperAdminSettings:Email"];
        string? rawPassword = _configuration["SuperAdminSettings:Password"];

        var existingSuperAdmin = await _context.Users
            .FirstOrDefaultAsync(u => u.Role == UserRole.SuperAdmin, cancellationToken);

        if (existingSuperAdmin != null)
        {
            if (!string.IsNullOrWhiteSpace(rawPassword))
            {
                string newHash = _passwordHasher.HashPassword(rawPassword);
                existingSuperAdmin.UpdatePassword(new PasswordHash(newHash));
                await _context.SaveChangesAsync(cancellationToken);
                LogSuperAdminPasswordUpdated(_logger, existingSuperAdmin.Email.Value);
            }
            else
            {
                LogSuperAdminAlreadyExists(_logger);
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException(
                "[SuperAdminSeeder] La clave 'SuperAdminSettings:Email' no esta configurada.");

        if (string.IsNullOrWhiteSpace(rawPassword))
            throw new InvalidOperationException(
                "[SuperAdminSeeder] La clave 'SuperAdminSettings:Password' no esta configurada.");

        string firstName = _configuration["SuperAdminSettings:FirstName"] ?? "Super";
        string lastName  = _configuration["SuperAdminSettings:LastName"]  ?? "Admin";

        string passwordHash = _passwordHasher.HashPassword(rawPassword);

        var superAdmin = User.Create(
            email:        email,
            passwordHash: passwordHash,
            role:         UserRole.SuperAdmin,
            tenantId:     null,
            firstName:    firstName,
            lastName:     lastName);

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
