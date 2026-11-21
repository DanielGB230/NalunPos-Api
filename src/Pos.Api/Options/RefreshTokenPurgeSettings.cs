using System.ComponentModel.DataAnnotations;

namespace Pos.Api.Options;

/// <summary>
/// Configuración del BackgroundService de purga de refresh tokens (F1.G.6 / ADR 0033).
/// Se registra con <c>ValidateDataAnnotations()</c> + <c>ValidateOnStart()</c>.
/// </summary>
public class RefreshTokenPurgeSettings
{
    public const string SectionName = "RefreshTokenPurge";

    /// <summary>Activa o desactiva la programación del worker. Por defecto activo.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Intervalo entre ejecuciones de purga. Por defecto 60 minutos.</summary>
    [Range(1, 1440, ErrorMessage = "IntervalMinutes debe estar entre 1 y 1440 minutos.")]
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Retención de tokens revocados/expirados. Por defecto 7 días.
    /// La retención sostiene la detección de reuso de la familia.
    /// </summary>
    [Range(1, 90, ErrorMessage = "RetentionDays debe estar entre 1 y 90 días.")]
    public int RetentionDays { get; set; } = 7;

    /// <summary>Tamaño máximo de cada lote de borrado. Por defecto 1000.</summary>
    [Range(1, 5000, ErrorMessage = "BatchSize debe estar entre 1 y 5000.")]
    public int BatchSize { get; set; } = 1000;

    /// <summary>Retención como <see cref="TimeSpan"/>.</summary>
    public TimeSpan Retention => TimeSpan.FromDays(RetentionDays);
}
