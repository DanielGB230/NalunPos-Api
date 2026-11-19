using System.ComponentModel.DataAnnotations;

namespace Pos.Infrastructure;

/// <summary>
/// Opciones de vigencia del access token JWT.
/// Se enlazan desde la misma sección <c>JwtSettings</c> que Secret/Issuer/Audience, sin duplicarlos:
/// esta clase solo aporta la duración validada en el arranque (ValidateOnStart).
/// </summary>
public class AccessTokenLifetimeOptions
{
    public const string SectionName = "JwtSettings";

    /// <summary>
    /// Duración del access token en minutos. Por defecto 60.
    /// Fuente única usada por el generador para el claim <c>exp</c> y la expiración devuelta al login.
    /// </summary>
    [Range(5, 120, ErrorMessage = "La duración del access token debe estar entre 5 y 120 minutos.")]
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}
