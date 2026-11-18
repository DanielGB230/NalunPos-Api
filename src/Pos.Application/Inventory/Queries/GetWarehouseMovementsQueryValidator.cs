using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Inventory.Queries;

public class GetWarehouseMovementsQueryValidator : AbstractValidator<GetWarehouseMovementsQuery>
{
    public GetWarehouseMovementsQueryValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("El WarehouseId es requerido.");
        RuleFor(x => x.PageNumber).ApplyPageNumberRule();
        RuleFor(x => x.PageSize).ApplyPageSizeRule();
        RuleFor(x => x.StartDate).ApplyUtcInstantRule();
        RuleFor(x => x.EndDate).ApplyUtcInstantRule();
    }
}
