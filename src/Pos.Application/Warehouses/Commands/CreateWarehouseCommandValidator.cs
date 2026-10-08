using FluentValidation;
using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Warehouses.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Warehouses.Commands;

public class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("El ID de la sucursal es requerido.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del almacén es requerido.")
            .MaximumLength(100).WithMessage("El nombre del almacén no puede exceder los 100 caracteres.");
    }
}
