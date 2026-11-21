using System.Net;

namespace Pos.IntegrationTests.Support;

/// <summary>
/// Qué se espera de la cabecera <c>Set-Cookie</c> de la cookie de refresh.
/// </summary>
public enum RefreshCookieExpectation
{
    /// <summary>La respuesta emite la cookie con un token opaco no vacío.</summary>
    Issued,

    /// <summary>La respuesta emite la cookie de limpieza (Max-Age=0 o Expires epoch).</summary>
    Cleared,

    /// <summary>La respuesta NO emite la cookie.</summary>
    Absent
}

/// <summary>
/// Único punto de verdad de las pruebas de integración para verificar la cabecera
/// <c>Set-Cookie</c> de la cookie de refresh <c>nalunpos_rt</c>.
/// Evita repetir el parseo de cabeceras en cada prueba (ADR 0032).
/// </summary>
public static class RefreshTokenCookieAssertions
{
    /// <summary>Nombre de la cookie de refresh del contrato HTTP.</summary>
    public const string CookieName = "nalunpos_rt";

    /// <summary>
    /// Valida la cabecera <c>Set-Cookie</c> de la cookie de refresh según la expectativa
    /// y devuelve el valor crudo de la cookie cuando la respuesta la emite
    /// (<see cref="RefreshCookieExpectation.Issued"/> o <see cref="RefreshCookieExpectation.Cleared"/>).
    /// </summary>
    public static string? AssertSetCookie(HttpResponseMessage response, RefreshCookieExpectation expectation)
    {
        ArgumentNullException.ThrowIfNull(response);

        string? setCookie = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith($"{CookieName}=", StringComparison.OrdinalIgnoreCase))
            : null;

        if (expectation == RefreshCookieExpectation.Absent)
        {
            Assert.True(
                setCookie is null,
                $"La respuesta no debe emitir una cookie {CookieName}. Set-Cookie: {setCookie ?? "<ninguna>"}");
            return null;
        }

        Assert.True(
            setCookie is not null,
            $"La respuesta debe emitir la cookie {CookieName}. Expectativa: {expectation}.");

        string cookieValue = setCookie!.Split(';')[0][(CookieName.Length + 1)..];

        if (expectation == RefreshCookieExpectation.Issued)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(cookieValue),
                $"La cookie {CookieName} debe transportar un token opaco no vacío. Set-Cookie: {setCookie}");
            return setCookie;
        }

        Assert.True(
            string.IsNullOrEmpty(cookieValue),
            $"La cookie de limpieza {CookieName} debe llegar vacía. Set-Cookie: {setCookie}");
        Assert.True(
            setCookie.Contains("max-age=0", StringComparison.OrdinalIgnoreCase) ||
            setCookie.Contains("expires=thu, 01 jan 1970", StringComparison.OrdinalIgnoreCase),
            $"La cookie de limpieza {CookieName} debe expirar de inmediato. Set-Cookie: {setCookie}");

        return setCookie;
    }
}
