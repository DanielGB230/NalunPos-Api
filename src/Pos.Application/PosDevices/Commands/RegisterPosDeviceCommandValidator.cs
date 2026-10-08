using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.PosDevices.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.Common;

namespace Pos.Application.PosDevices.Commands;

public class RegisterPosDeviceCommandValidator : AbstractValidator<RegisterPosDeviceCommand>
{
    public RegisterPosDeviceCommandValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("El ID de la sucursal es requerido.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del dispositivo es requerido.");

        RuleFor(x => x.SerialNumber)
            .NotEmpty().WithMessage("El número de serie o MAC del dispositivo es requerido.");
    }
}
