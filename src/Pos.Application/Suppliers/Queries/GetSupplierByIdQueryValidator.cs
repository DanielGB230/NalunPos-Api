using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Suppliers.Queries;

public class GetSupplierByIdQueryValidator : AbstractValidator<GetSupplierByIdQuery>
{
    public GetSupplierByIdQueryValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
