using System.ComponentModel.DataAnnotations;

namespace Pos.Application.Common.Options;

/// <summary>
/// Opciones del refresh token (duración y nombre de cookie).
/// Consumida por el handler de rotación en la capa Application.
/// Se registra y valida en Program.cs con <c>ValidateDataAnnotations()</c> +
/// <c>ValidateOnStart()</c>.
/// </summary>
public class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    /// <summary>
    /// Vigencia del refresh token en días. Por defecto 7.
    /// </summary>
    [Range(1, 365, ErrorMessage = "RefreshTokenDays debe estar entre 1 y 365.")]
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>
    /// Nombre de la cookie HttpOnly que transporta el refresh token.
    /// </summary>
    [Required(ErrorMessage = "CookieName es requerido.")]
    [MinLength(1, ErrorMessage = "CookieName no puede estar vacío.")]
    public string CookieName { get; set; } = "nalunpos_rt";

    /// <summary>
    /// Vigencia del refresh token como <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(RefreshTokenDays);

    /// <summary>
    /// Ventana de tolerancia, en segundos, para clasificar como rotación concurrente
    /// un token revocado por <c>Rotated</c>. Por defecto 10; 0 desactiva la ventana.
    /// </summary>
    [Range(0, 60, ErrorMessage = "RotationLeewaySeconds debe estar entre 0 y 60.")]
    public int RotationLeewaySeconds { get; set; } = 10;

    /// <summary>
    /// Ventana de tolerancia como <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan RotationLeeway => TimeSpan.FromSeconds(RotationLeewaySeconds);
}
