using Pos.Application.Common.Attributes;
using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Roles.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.Common;

namespace Pos.Application.Roles.Commands;

public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del rol es requerido.")
            .Length(2, 50).WithMessage("El nombre debe contener entre 2 y 50 caracteres.");
    }
}
