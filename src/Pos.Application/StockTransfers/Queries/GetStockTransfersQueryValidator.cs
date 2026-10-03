using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.StockTransfers.Queries;

public class GetStockTransfersQueryValidator : AbstractValidator<GetStockTransfersQuery>
{
    public GetStockTransfersQueryValidator()
    {
        RuleFor(x => x.PageNumber).ApplyPageNumberRule();
        RuleFor(x => x.PageSize).ApplyPageSizeRule();
    }
}
