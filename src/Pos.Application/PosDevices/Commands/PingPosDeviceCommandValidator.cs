using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.PosDevices.Commands;

public class PingPosDeviceCommandValidator : AbstractValidator<PingPosDeviceCommand>
{
    public PingPosDeviceCommandValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
