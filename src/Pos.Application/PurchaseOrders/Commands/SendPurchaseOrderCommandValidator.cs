using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.PurchaseOrders.Commands;

public class SendPurchaseOrderCommandValidator : AbstractValidator<SendPurchaseOrderCommand>
{
    public SendPurchaseOrderCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId).ApplyRequiredIdRule();
    }
}
