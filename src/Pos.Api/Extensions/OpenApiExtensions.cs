using Microsoft.OpenApi;

namespace Pos.Api.Extensions;

public static class OpenApiExtensions
{
    public static IServiceCollection AddCustomOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                var scheme = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Ingresa tu token JWT para acceder a los endpoints protegidos."
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["Bearer"] = scheme;

                var schemeRef = new OpenApiSecuritySchemeReference("Bearer", document);

                var requirement = new OpenApiSecurityRequirement
                {
                    [schemeRef] = new List<string>()
                };

                document.Security ??= new List<OpenApiSecurityRequirement>();
                document.Security.Add(requirement);

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
