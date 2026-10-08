using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.PurchaseOrders.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Application.Common.Validation;

namespace Pos.Application.PurchaseOrders.Commands;

public class ReceivePurchaseOrderCommandValidator : AbstractValidator<ReceivePurchaseOrderCommand>
{
    public ReceivePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty().WithMessage("El ID de la orden es requerido.");
        RuleFor(x => x.ReceivedLines).NotEmpty().WithMessage("Debe especificar al menos una línea a recibir.");
        RuleForEach(x => x.ReceivedLines).ChildRules(l =>
        {
            l.RuleFor(i => i.ProductId).NotEmpty().WithMessage(IdValidationExtensions.RequiredIdErrorMessage);
            l.RuleFor(i => i.ReceivedQuantity).GreaterThan(0).WithMessage("La cantidad recibida debe ser mayor a 0.");
        });
    }
}
