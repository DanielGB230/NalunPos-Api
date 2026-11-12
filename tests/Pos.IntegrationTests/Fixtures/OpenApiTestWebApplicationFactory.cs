using Microsoft.AspNetCore.Hosting;
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
/// habilitando de forma explícita el endpoint /openapi/v1.json mediante la clave de configuración OpenApi:ExposeEndpoint
/// y aislando su base de datos in-memory para prevenir condiciones de carrera en siembra concurrente de pruebas.
/// </summary>
public sealed class OpenApiTestWebApplicationFactory : CustomWebApplicationFactory
{
    private static readonly string s_dbName = $"NalunPosDb_OpenApi_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("OpenApi:ExposeEndpoint", "true");

        builder.ConfigureTestServices(services =>
        {
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
}
