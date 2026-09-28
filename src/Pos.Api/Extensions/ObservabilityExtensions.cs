using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace Pos.Api.Extensions;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddCustomObservability(this IServiceCollection services, ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.JsonWriterOptions = new JsonWriterOptions
            {
                Indented = false
            };
        });

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource("Pos.Api")
                .AddAspNetCoreInstrumentation(options => options.RecordException = true)
                .AddHttpClientInstrumentation()
                .AddConsoleExporter());

        return services;
    }
}
