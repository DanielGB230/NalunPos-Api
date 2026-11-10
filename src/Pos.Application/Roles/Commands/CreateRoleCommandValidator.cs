using FluentValidation;
using Pos.Domain.Common;

namespace Pos.Application.Roles.Commands;

public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del rol es requerido.")
            .Length(2, 50).WithMessage("El nombre debe contener entre 2 y 50 caracteres.")
            .Must(name => !RoleNames.IsReserved(name))
            .WithErrorCode(RoleErrorCodes.NameReserved)
            .WithMessage("El nombre del rol está reservado para roles del sistema y no puede ser utilizado.");
    }
}
