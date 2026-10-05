using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.PosDevices.Queries;

public class GetPosDevicesByBranchQueryValidator : AbstractValidator<GetPosDevicesByBranchQuery>
{
    public GetPosDevicesByBranchQueryValidator()
    {
        RuleFor(x => x.BranchId).ApplyRequiredIdRule();
    }
}
