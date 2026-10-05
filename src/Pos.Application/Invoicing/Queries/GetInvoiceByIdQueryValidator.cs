using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Invoicing.Queries;

public class GetInvoiceByIdQueryValidator : AbstractValidator<GetInvoiceByIdQuery>
{
    public GetInvoiceByIdQueryValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
