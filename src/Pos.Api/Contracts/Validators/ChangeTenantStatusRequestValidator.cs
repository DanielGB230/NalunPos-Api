using FluentValidation;
using Pos.Api.Contracts.Requests;
using Pos.Domain.Enums;

namespace Pos.Api.Contracts.Validators;

/// <summary>
/// Validador de borde para el cambio de estado de un Tenant.
/// Rechaza cualquier estado que no sea <see cref="TenantStatus.Active"/> o <see cref="TenantStatus.Suspended"/>.
/// </summary>
public class ChangeTenantStatusRequestValidator : AbstractValidator<ChangeTenantStatusRequest>
{
    public const string InvalidStatusCode = "Tenant.InvalidStatus";

    public ChangeTenantStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => status is TenantStatus.Active or TenantStatus.Suspended)
            .WithMessage("El estado del tenant solo admite 'Active' o 'Suspended'.")
            .WithErrorCode(InvalidStatusCode);
    }
}
