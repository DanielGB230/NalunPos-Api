namespace Pos.Api.Options;

/// <summary>
/// Configuración de CORS.
/// </summary>
public class CorsSettings
{
    public const string SectionName = "CorsSettings";

    /// <summary>
    /// Lista de orígenes permitidos. Fail-closed: si está vacío, no se permiten peticiones cross-origin.
    /// </summary>
    public List<string> AllowedOrigins { get; set; } = [];
}
