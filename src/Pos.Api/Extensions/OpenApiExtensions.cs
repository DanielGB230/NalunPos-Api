using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.OpenApi;

namespace Pos.Api.Extensions;

public static class OpenApiExtensions
{
    public static IServiceCollection AddCustomOpenApi(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

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

                if (document.Components.Schemas != null &&
                    document.Components.Schemas.TryGetValue("ProblemDetails", out var problemSchema) &&
                    problemSchema is OpenApiSchema concreteProblemSchema)
                {
                    concreteProblemSchema.Properties ??= new Dictionary<string, IOpenApiSchema>();
                    if (!concreteProblemSchema.Properties.ContainsKey("code"))
                    {
                        concreteProblemSchema.Properties["code"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String | JsonSchemaType.Null,
                            Description = "Código de error de dominio RFC 9457 (ADR 0023)."
                        };
                    }
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
