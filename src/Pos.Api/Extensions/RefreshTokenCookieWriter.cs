using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Pos.Application.Common.Options;
using Pos.Domain.Common;

namespace Pos.Api.Extensions;

/// <summary>
/// Escritor de la cookie HttpOnly <c>nalunpos_rt</c> que transporta el refresh token.
/// Atributos:
///   - HttpOnly: no accesible desde JavaScript (mitiga XSS).
///   - SameSite=Strict: no se envía en peticiones cross-site (mitiga CSRF).
///   - Path=/api/v1/auth: acotada solo a los endpoints de autenticación.
///   - Secure=true fuera de Development; en Development sigue la petición (HTTP permitido).
///   - Max-Age: vigencia configurada en <see cref="RefreshTokenOptions.RefreshTokenDays"/>.
/// </summary>
public class RefreshTokenCookieWriter
{
    private readonly RefreshTokenOptions _options;
    private readonly IHostEnvironment _env;

    /// <summary>Ruta de la cookie (acotada a los endpoints de autenticación).</summary>
    public const string CookiePath = "/api/v1/auth";

    public RefreshTokenCookieWriter(RefreshTokenOptions options, IHostEnvironment env)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _env = env ?? throw new ArgumentNullException(nameof(env));
    }

    /// <summary>Nombre de la cookie configurado en <see cref="RefreshTokenOptions.CookieName"/>.</summary>
    public string CookieName => _options.CookieName;

    /// <summary>Escribe la cookie con el refresh token y su vigencia.</summary>
    public void Write(HttpResponse response, string token, int maxAgeSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            MaxAge = TimeSpan.FromSeconds(maxAgeSeconds),
            Secure = !_env.IsDevelopment()
        };

        response.Cookies.Append(_options.CookieName, token, cookieOptions);
    }

    /// <summary>
    /// Borra la cookie solo si la sesión murió definitivamente según la política (ADR 0032).
    /// </summary>
    public void ClearIfSessionEnded(HttpResponse response, DomainError error)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(error);

        if (RefreshTokenCookiePolicy.ShouldClear(error))
        {
            Clear(response);
        }
    }

    /// <summary>
    /// Borra la cookie siempre (logout idempotente, ADR 0032).
    /// </summary>
    public void ClearOnLogout(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        Clear(response);
    }

    /// <summary>
    /// Borra la cookie de refresh token (expiración inmediata).
    /// Privado: solo se accede a través de las operaciones gobernadas por la política.
    /// </summary>
    private void Clear(HttpResponse response)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            MaxAge = TimeSpan.Zero
        };

        response.Cookies.Append(_options.CookieName, string.Empty, cookieOptions);
    }
}
