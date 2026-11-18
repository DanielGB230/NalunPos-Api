using FluentValidation;

namespace Pos.Application.Platform.Tenants.Commands.SuspendTenant;

/// <summary>
/// Validador del comando de suspensión de Tenant.
/// </summary>
public class SuspendTenantCommandValidator : AbstractValidator<SuspendTenantCommand>
{
    public SuspendTenantCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID del tenant es requerido.");
    }
}
