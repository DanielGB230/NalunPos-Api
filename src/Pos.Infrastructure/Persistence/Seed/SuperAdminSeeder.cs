using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Seed;

/// <summary>
/// Proceso de infraestructura para la siembra idempotente del primer SuperAdmin del sistema.
///
/// Principios de seguridad:
///   - Las credenciales se leen EXCLUSIVAMENTE desde IConfiguration / dotnet user-secrets.
///   - NUNCA hay fallback con valores hardcodeados ni contraseñas por defecto.
///   - Si Email o Password no están configurados, el sistema falla de forma
///     explícita y detectable (fail-fast) en lugar de sembrar con valores inseguros.
///
/// Configuración requerida (user-secrets en dev, variables de entorno en producción):
///   SuperAdminSettings:Email     → email del superadmin
///   SuperAdminSettings:Password  → contraseña (mínimo 12 caracteres recomendado)
///   SuperAdminSettings:FirstName → nombre   (opcional, default: "Super")
///   SuperAdminSettings:LastName  → apellido (opcional, default: "Admin")
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
        // Idempotencia estricta: verificar si ya existe un SuperAdmin
        bool superAdminExists = await _context.Users
            .AnyAsync(u => u.Role == UserRole.SuperAdmin, cancellationToken);

        if (superAdminExists)
        {
            LogSuperAdminAlreadyExists(_logger);
            return;
        }

        // Leer credenciales desde IConfiguration (user-secrets / env vars / appsettings)
        string? email = _configuration["SuperAdminSettings:Email"];
        string? rawPassword = _configuration["SuperAdminSettings:Password"];

        // Fail-fast: credenciales obligatorias no configuradas
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException(
                "[SuperAdminSeeder] La clave 'SuperAdminSettings:Email' no esta configurada. " +
                "Configurala en user-secrets o variables de entorno.");

        if (string.IsNullOrWhiteSpace(rawPassword))
            throw new InvalidOperationException(
                "[SuperAdminSeeder] La clave 'SuperAdminSettings:Password' no esta configurada. " +
                "Configurala en user-secrets o variables de entorno.");

        // Campos opcionales con defaults seguros y no sensibles
        string firstName = _configuration["SuperAdminSettings:FirstName"] ?? "Super";
        string lastName  = _configuration["SuperAdminSettings:LastName"]  ?? "Admin";

        // Generar hash seguro (Argon2id PHC format)
        string passwordHash = _passwordHasher.HashPassword(rawPassword);

        // Crear usuario respetando invariantes de dominio
        // SuperAdmin: TenantId = null (no pertenece a ningún tenant)
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

    // ── LoggerMessage Source Generators (zero-allocation, CA1873-compliant) ──

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SuperAdminSeeder: SuperAdmin ya existe. Siembra omitida.")]
    private static partial void LogSuperAdminAlreadyExists(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SuperAdminSeeder: SuperAdmin sembrado exitosamente. Email={Email}")]
    private static partial void LogSuperAdminSeeded(ILogger logger, string email);
}
