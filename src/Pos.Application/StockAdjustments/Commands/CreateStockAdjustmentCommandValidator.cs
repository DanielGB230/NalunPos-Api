using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.StockAdjustments.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Application.Common.Validation;

namespace Pos.Application.StockAdjustments.Commands;

public class CreateStockAdjustmentCommandValidator : AbstractValidator<CreateStockAdjustmentCommand>
{
    public CreateStockAdjustmentCommandValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("El ID del almacén es requerido.");
        RuleFor(x => x.Reason).IsInEnum().WithMessage("El motivo del ajuste es requerido.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("El ajuste debe tener al menos una línea.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(i => i.ProductId).NotEmpty().WithMessage(IdValidationExtensions.RequiredIdErrorMessage);
            l.RuleFor(i => i.Quantity).NotEqual(0).WithMessage("La cantidad de ajuste no puede ser cero.");
        });
    }
}
