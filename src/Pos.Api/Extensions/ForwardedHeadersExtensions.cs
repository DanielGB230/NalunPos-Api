using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Pos.Api.Options;

namespace Pos.Api.Extensions;

public static class ForwardedHeadersExtensions
{
    /// <summary>
    /// Configura UseForwardedHeaders con proxies/redes conocidas desde configuración.
    /// Fail-closed: sin configuración, solo se confía en loopback (127.0.0.1, ::1).
    /// NUNCA se limpian KnownProxies/KnownIPNetworks — eso abriría la IP-spoofing total.
    /// </summary>
    public static WebApplication UseCustomForwardedHeaders(this WebApplication app)
    {
        var settings = app.Configuration
            .GetSection(ForwardedHeadersSettings.SectionName)
            .Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();

        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = settings.ForwardLimit
            // Los valores por defecto incluyen loopback (127.0.0.1, ::1). No se limpian.
        };

        // Agregar proxies individuales conocidos desde configuración
        foreach (var proxyIp in settings.KnownProxies)
        {
            if (IPAddress.TryParse(proxyIp, out var address))
            {
                options.KnownProxies.Add(address);
            }
        }

        // Agregar redes conocidas (CIDR) desde configuración — usar KnownIPNetworks (.NET 10)
        foreach (var cidr in settings.KnownNetworks)
        {
            if (System.Net.IPNetwork.TryParse(cidr, out var network))
            {
                options.KnownIPNetworks.Add(network);
            }
        }

        app.UseForwardedHeaders(options);
        return app;
    }
}
