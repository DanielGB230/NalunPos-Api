namespace Pos.Api.Extensions;

public static class CorsExtensions
{
    public static IServiceCollection AddCustomCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins(
                          "http://localhost:4200",
                          "http://localhost:4201",
                          "https://localhost:4200",
                          "https://localhost:4201"
                      )
                      .SetIsOriginAllowed(_ => true)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        return services;
    }
}
