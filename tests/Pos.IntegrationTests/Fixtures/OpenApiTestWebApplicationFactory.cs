using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Interceptors;

namespace Pos.IntegrationTests.Fixtures;

/// <summary>
/// WebApplicationFactory especializado para la extracción y pruebas de contrato del documento OpenAPI v1.
/// Hereda de <see cref="CustomWebApplicationFactory"/> para reutilizar el entorno in-memory y servicios aislados,
/// registrando un <see cref="IStartupFilter"/> de pruebas para mapear el endpoint /openapi/v1.json de forma exclusiva
/// en el pipeline de tests sin flags de configuración en producción ni alterar src/Pos.Api/Program.cs,
/// y aislando su base de datos in-memory para prevenir condiciones de carrera en siembra concurrente de pruebas.
/// </summary>
public sealed class OpenApiTestWebApplicationFactory : CustomWebApplicationFactory
{
    private static readonly string s_dbName = $"NalunPosDb_OpenApi_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AddTransient<IStartupFilter, OpenApiTestStartupFilter>();

            services.RemoveAll<DbContextOptions<PosDbContext>>();
            services.RemoveAll<DbContextOptions>();
            var efConfigs = services
                .Where(d => d.ServiceType.IsGenericType &&
                            d.ServiceType.GetGenericTypeDefinition().FullName == "Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration`1" &&
                            d.ServiceType.GetGenericArguments()[0] == typeof(PosDbContext))
                .ToList();
            foreach (var d in efConfigs) services.Remove(d);

            services.AddDbContext<PosDbContext>((provider, options) =>
            {
                options.UseInMemoryDatabase(s_dbName);
                options.AddPosSaveChangesInterceptors(provider);
            });
        });
    }

    private sealed class OpenApiTestStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                next(app);
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapOpenApi().AllowAnonymous();
                });
            };
        }
    }
}
