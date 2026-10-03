using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Notifications.Queries;

public class GetUserNotificationsQueryValidator : AbstractValidator<GetUserNotificationsQuery>
{
    public GetUserNotificationsQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("El UserId es requerido.");
        RuleFor(x => x.PageNumber).ApplyPageNumberRule();
        RuleFor(x => x.PageSize).ApplyPageSizeRule();
    }
}
