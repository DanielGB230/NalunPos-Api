using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Invoicing.Commands;

public class ReconcileInvoiceCommandValidator : AbstractValidator<ReconcileInvoiceCommand>
{
    public ReconcileInvoiceCommandValidator()
    {
        RuleFor(x => x.InvoiceId).ApplyRequiredIdRule();
    }
}
