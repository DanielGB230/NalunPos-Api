using FluentValidation;

namespace Pos.Application.Platform.Tenants.Commands.CreateTenant;

/// <summary>
/// Validador para el comando de creación de Tenant y su Administrador inicial.
/// </summary>
public class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("El nombre del tenant es requerido.");

        RuleFor(x => x.DocumentNumber)
            .NotEmpty()
            .WithMessage("El número de documento de identificación fiscal (RUC/NIT/RFC) es requerido.");

        RuleFor(x => x.AdminEmail)
            .NotEmpty()
            .WithMessage("El correo electrónico del administrador es requerido.")
            .EmailAddress()
            .WithMessage("El correo electrónico del administrador no tiene un formato válido.");

        RuleFor(x => x.AdminPassword)
            .NotEmpty()
            .WithMessage("La contraseña del administrador es requerida.")
            .MinimumLength(8)
            .WithMessage("La contraseña del administrador debe tener al menos 8 caracteres.")
            .Matches(@"[A-Z]")
            .WithMessage("La contraseña del administrador debe contener al menos una letra mayúscula.")
            .Matches(@"[0-9]")
            .WithMessage("La contraseña del administrador debe contener al menos un número.")
            .Matches(@"[^a-zA-Z0-9]")
            .WithMessage("La contraseña del administrador debe contener al menos un carácter especial.");
    }
}
