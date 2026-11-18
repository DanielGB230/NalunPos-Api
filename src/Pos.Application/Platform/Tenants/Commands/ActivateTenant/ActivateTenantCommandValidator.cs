using FluentValidation;

namespace Pos.Application.Platform.Tenants.Commands.ActivateTenant;

/// <summary>
/// Validador del comando de activación de Tenant.
/// </summary>
public class ActivateTenantCommandValidator : AbstractValidator<ActivateTenantCommand>
{
    public ActivateTenantCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID del tenant es requerido.");
    }
}
