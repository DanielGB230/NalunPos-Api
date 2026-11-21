using System.ComponentModel.DataAnnotations;

namespace Pos.Application.Common.Options;

/// <summary>
/// Opciones del refresh token (duración deslizante, vigencia absoluta de la familia
/// y nombre de cookie). Consumida por los handlers de login y rotación en Application.
/// Se registra y valida en Program.cs con <c>ValidateDataAnnotations()</c> +
/// <c>ValidateOnStart()</c>.
/// </summary>
public class RefreshTokenOptions : IValidatableObject
{
    public const string SectionName = "RefreshToken";

    /// <summary>
    /// Vigencia deslizante del refresh token en días. Por defecto 7.
    /// </summary>
    [Range(1, 365, ErrorMessage = "RefreshTokenDays debe estar entre 1 y 365.")]
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>
    /// Vigencia absoluta de la familia de refresh tokens en días. Por defecto 30.
    /// Debe ser mayor o igual que <see cref="RefreshTokenDays"/>.
    /// </summary>
    [Range(1, 365, ErrorMessage = "FamilyAbsoluteDays debe estar entre 1 y 365.")]
    public int FamilyAbsoluteDays { get; set; } = 30;

    /// <summary>
    /// Nombre de la cookie HttpOnly que transporta el refresh token.
    /// </summary>
    [Required(ErrorMessage = "CookieName es requerido.")]
    [MinLength(1, ErrorMessage = "CookieName no puede estar vacío.")]
    public string CookieName { get; set; } = "nalunpos_rt";

    /// <summary>
    /// Vigencia deslizante del refresh token como <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(RefreshTokenDays);

    /// <summary>
    /// Vigencia absoluta de la familia como <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan FamilyAbsoluteLifetime => TimeSpan.FromDays(FamilyAbsoluteDays);

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

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FamilyAbsoluteDays < RefreshTokenDays)
        {
            yield return new ValidationResult(
                $"FamilyAbsoluteDays ({FamilyAbsoluteDays}) debe ser mayor o igual que RefreshTokenDays ({RefreshTokenDays}), de modo que la vigencia absoluta de la familia nunca sea inferior a la deslizante.",
                [nameof(FamilyAbsoluteDays), nameof(RefreshTokenDays)]);
        }
    }
}
