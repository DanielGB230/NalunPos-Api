using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Platform.Tenants.Queries.GetTenants;

public class GetTenantsQueryValidator : AbstractValidator<GetTenantsQuery>
{
    public GetTenantsQueryValidator()
    {
        RuleFor(x => x.PageNumber).ApplyPageNumberRule();
        RuleFor(x => x.PageSize).ApplyPageSizeRule();
    }
}
