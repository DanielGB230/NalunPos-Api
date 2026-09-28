using System.ComponentModel.DataAnnotations;

namespace Pos.Api.Options;

/// <summary>
/// Configuración JWT. Si falta cualquier campo, la app NO arranca (ValidateOnStart).
/// El secreto NUNCA debe tener valor por defecto en código — debe venir de
/// user-secrets o variables de entorno.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    /// <summary>
    /// Clave secreta de firma. Mínimo 32 bytes en UTF-8 (256 bits).
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [MinLength(32)]
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Emisor del token (iss).
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Audiencia del token (aud).
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Audience { get; set; } = string.Empty;
}
