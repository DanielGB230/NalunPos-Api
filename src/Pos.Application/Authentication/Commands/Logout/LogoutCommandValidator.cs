using FluentValidation;

namespace Pos.Application.Authentication.Commands.Logout;

/// <summary>
/// Validador de <see cref="LogoutCommand"/>.
/// No hay campos obligatorios (el token es opcional por idempotencia).
/// Se mantiene el archivo propio por la guarda de arquitectura.
/// </summary>
public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        // Sin reglas: el refresh token es opcional (logout idempotente).
    }
}
