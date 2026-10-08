using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.Inventory.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Inventory.Commands;

public class RecordInventoryMovementCommandValidator : AbstractValidator<RecordInventoryMovementCommand>
{
    public RecordInventoryMovementCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("El ID del producto es requerido.");

        RuleFor(x => x.Quantity)
            .NotEqual(0).WithMessage("La cantidad de movimiento no puede ser cero.");

        RuleFor(x => x.MovementType)
            .IsInEnum().WithMessage("El tipo de movimiento especificado no es válido.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Las notas no pueden exceder 500 caracteres.");
    }
}
