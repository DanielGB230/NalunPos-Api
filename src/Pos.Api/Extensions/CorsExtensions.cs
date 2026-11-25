using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Pos.Api.Options;

namespace Pos.Api.Extensions;

/// <summary>
/// Extensión para registrar la política CORS "AllowFrontend" desde las opciones
/// <see cref="CorsSettings"/> ya validadas en <c>Program.cs</c> (ValidateOnStart).
/// </summary>
public static class CorsExtensions
{
    /// <summary>Nombre de la política de CORS (contrato con el middleware <c>app.UseCors</c>).</summary>
    public const string AllowFrontendPolicy = "AllowFrontend";

    /// <summary>Métodos permitidos por la política (coinciden con los controladores).</summary>
    private static readonly string[] s_allowedMethods = ["GET", "POST", "PATCH", "OPTIONS"];

    /// <summary>
    /// Registra la política de CORS desde las opciones <b>ya validadas</b> de
    /// <see cref="CorsSettings"/> (ValidateOnStart). La extensión no lee configuración
    /// cruda ni duplica la comprobación de la lista: la fuente única de verdad son las
    /// opciones validadas en <c>Program.cs</c>.
    /// </summary>
    public static IServiceCollection AddCustomCors(this IServiceCollection services)
    {
        services.AddCors();

        // Nombre por defecto: así lo resuelve DefaultCorsPolicyProvider al evaluar
        // app.UseCors("AllowFrontend") (mismo registro que AddCors(Action<CorsOptions>)).
        services.AddOptions<CorsOptions>()
            .Configure<IOptions<CorsSettings>>((corsOptions, settingsAccessor) =>
            {
                corsOptions.AddPolicy(AllowFrontendPolicy, policy => policy
                    .WithOrigins(settingsAccessor.Value.AllowedOrigins.ToArray())
                    .AllowAnyHeader()
                    .WithMethods(s_allowedMethods)
                    .AllowCredentials());
            });

        return services;
    }
}