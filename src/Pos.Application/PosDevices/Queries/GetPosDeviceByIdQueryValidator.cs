using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.PosDevices.Queries;

public class GetPosDeviceByIdQueryValidator : AbstractValidator<GetPosDeviceByIdQuery>
{
    public GetPosDeviceByIdQueryValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
