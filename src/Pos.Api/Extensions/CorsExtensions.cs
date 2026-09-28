using Pos.Api.Options;

namespace Pos.Api.Extensions;

public static class CorsExtensions
{
    public static IServiceCollection AddCustomCors(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>() ?? new CorsSettings();

        // Filtrar vacíos o nulos
        settings.AllowedOrigins = settings.AllowedOrigins.Where(o => !string.IsNullOrWhiteSpace(o)).ToList();

        if (settings.AllowedOrigins.Count == 0)
        {
            throw new InvalidOperationException("CorsSettings:AllowedOrigins debe estar configurado con al menos un origen. No se permite SetIsOriginAllowed(_ => true).");
        }

        services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins(settings.AllowedOrigins.ToArray())
                      // ELIMINADO: .SetIsOriginAllowed(_ => true) por vulnerabilidad
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        return services;
    }
}
