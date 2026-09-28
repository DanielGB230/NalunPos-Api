namespace Pos.Api.Options;

/// <summary>
/// Opciones para configurar el manejo de cabeceras de proxy inverso (ForwardedHeaders).
/// Si no se configura, solo se confía en loopback (fail-closed by default).
/// </summary>
public class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>
    /// Máximas entradas de proxy a procesar en la cadena X-Forwarded-For.
    /// Limitar evita ataques de inyección de encabezados.
    /// </summary>
    public int ForwardLimit { get; set; } = 1;

    /// <summary>
    /// IPs de proxies conocidos y confiables. Si está vacío, solo se acepta loopback.
    /// Ejemplo: ["10.0.0.1", "192.168.1.1"]
    /// </summary>
    public List<string> KnownProxies { get; set; } = [];

    /// <summary>
    /// Redes conocidas y confiables en formato CIDR. Si está vacío, solo se acepta loopback.
    /// Ejemplo: ["10.0.0.0/8", "172.16.0.0/12"]
    /// </summary>
    public List<string> KnownNetworks { get; set; } = [];
}
