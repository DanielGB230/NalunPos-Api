namespace Pos.Api.Options;

/// <summary>
/// Configuración de CORS. Fail-closed: si la lista está vacía, no se permiten
/// peticiones cross-origin. Valida en el arranque mediante ValidateOnStart().
/// </summary>
public class CorsSettings
{
    public const string SectionName = "CorsSettings";

    /// <summary>Lista de orígenes permitidos. Fail-closed: vacío = ninguno.</summary>
    public List<string> AllowedOrigins { get; set; } = [];
}