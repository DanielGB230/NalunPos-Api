using FluentValidation;

namespace Pos.Application.Authentication.Queries.GetCurrentUser;

/// <summary>
/// Validador de <see cref="GetCurrentUserQuery"/>.
/// No hay campos de entrada que validar.
/// </summary>
public class GetCurrentUserQueryValidator : AbstractValidator<GetCurrentUserQuery>
{
    public GetCurrentUserQueryValidator()
    {
        // Sin reglas: la identidad se resuelve de los claims del JWT.
    }
}
