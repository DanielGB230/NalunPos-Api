using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.PurchaseOrders.Queries;

public class GetPurchaseOrdersQueryValidator : AbstractValidator<GetPurchaseOrdersQuery>
{
    public GetPurchaseOrdersQueryValidator()
    {
        RuleFor(x => x.PageNumber).ApplyPageNumberRule();
        RuleFor(x => x.PageSize).ApplyPageSizeRule();
    }
}
