using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Inventory.Queries;

public class GetInventoryHistoryQueryValidator : AbstractValidator<GetInventoryHistoryQuery>
{
    public GetInventoryHistoryQueryValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("El ProductId es requerido.");
        RuleFor(x => x.PageNumber).ApplyPageNumberRule();
        RuleFor(x => x.PageSize).ApplyPageSizeRule();
    }
}
