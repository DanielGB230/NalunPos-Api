using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Inventory.Queries;

public class GetProductStockQueryValidator : AbstractValidator<GetProductStockQuery>
{
    public GetProductStockQueryValidator()
    {
        RuleFor(x => x.ProductId).ApplyRequiredIdRule();
        RuleFor(x => x.WarehouseId).ApplyOptionalIdRule();
    }
}
