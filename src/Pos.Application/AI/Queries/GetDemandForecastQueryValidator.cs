using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.AI.Queries;

public class GetDemandForecastQueryValidator : AbstractValidator<GetDemandForecastQuery>
{
    public GetDemandForecastQueryValidator()
    {
        RuleFor(x => x.ProductId).ApplyRequiredIdRule();
        RuleFor(x => x.DaysAhead)
            .InclusiveBetween(DemandForecastRules.MinDaysAhead, DemandForecastRules.MaxDaysAhead)
            .WithMessage(DemandForecastRules.DaysAheadErrorMessage);
    }
}
