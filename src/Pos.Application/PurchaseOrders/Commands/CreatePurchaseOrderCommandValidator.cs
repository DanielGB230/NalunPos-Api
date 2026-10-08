using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.PurchaseOrders.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Pos.Application.Common.Validation;

namespace Pos.Application.PurchaseOrders.Commands;

public class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("El ID del proveedor es requerido.");
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("El ID del almacén destino es requerido.");
        RuleFor(x => x.OrderNumber)
            .NotEmpty().WithMessage("El número de orden es requerido.")
            .MaximumLength(50).WithMessage("El número de orden no puede exceder los 50 caracteres.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("La orden debe contener al menos una línea.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(i => i.ProductId).NotEmpty().WithMessage(IdValidationExtensions.RequiredIdErrorMessage);
            l.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor a 0.");
            l.RuleFor(i => i.UnitCostAmount).GreaterThanOrEqualTo(0).WithMessage("El costo unitario debe ser mayor o igual a 0.");
        });
    }
}
