using System.ComponentModel.DataAnnotations;

namespace Pos.Api.Options;

/// <summary>
/// Opciones de configuración para el SuperAdmin inicial (ValidateOnStart).
/// </summary>
public class SuperAdminSettings
{
    public const string SectionName = "SuperAdminSettings";

    [Required(AllowEmptyStrings = false, ErrorMessage = "El correo del SuperAdmin es requerido.")]
    [EmailAddress(ErrorMessage = "El correo del SuperAdmin debe ser un email válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "La contraseña del SuperAdmin es requerida.")]
    [MinLength(8, ErrorMessage = "La contraseña del SuperAdmin debe tener al menos 8 caracteres.")]
    public string Password { get; set; } = string.Empty;

    public string FirstName { get; set; } = "Super";

    public string LastName { get; set; } = "Admin";
}
