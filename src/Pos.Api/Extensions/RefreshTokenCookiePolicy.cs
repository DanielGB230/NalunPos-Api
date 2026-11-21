using Pos.Application.Authentication;
using Pos.Domain.Common;

namespace Pos.Api.Extensions;

/// <summary>
/// Regla única de limpieza de la cookie HttpOnly <c>nalunpos_rt</c> (ADR 0032).
///
/// La cookie solo se borra cuando la sesión murió de forma definitiva, es decir,
/// cuando el refresh devuelve <c>Auth.RefreshTokenInvalid</c> (token inexistente,
/// expirado, revocado por logout o por reuso fuera de la ventana de tolerancia).
///
/// Se conserva en los errores recuperables: 403 por estado de tenant, 401
/// <c>Auth.UserInvalid</c> (usuario inactivo), 409 <c>Auth.RefreshConcurrent</c>
/// y cualquier fallo de login (credenciales inválidas, tenant suspendido...).
/// </summary>
public static class RefreshTokenCookiePolicy
{
    /// <summary>
    /// Indica si la cookie de refresh debe limpiarse para el error dado.
    /// </summary>
    /// <param name="error">Error de negocio devuelto por login o refresh.</param>
    /// <returns><c>true</c> solo para <see cref="RefreshTokenErrors.InvalidCode"/>.</returns>
    public static bool ShouldClear(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return string.Equals(error.Code, RefreshTokenErrors.InvalidCode, StringComparison.Ordinal);
    }
}
