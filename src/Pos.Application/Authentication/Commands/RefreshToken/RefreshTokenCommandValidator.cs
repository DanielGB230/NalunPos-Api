using FluentValidation;

namespace Pos.Application.Authentication.Commands.RefreshToken;

/// <summary>
/// Validador de <see cref="RefreshTokenCommand"/>.
/// No hay campos de cuerpo que validar: el token se extrae de la cookie en el controlador.
/// Se mantiene el archivo propio por la guarda de arquitectura (un validador por command).
/// </summary>
public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        // Sin reglas de campo: el token se lee de la cookie HTTP (no del payload).
        // El handler falla closed si falta o es inválido.
    }
}
