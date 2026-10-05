using FluentValidation;

namespace Pos.Application.Common.Validation;

/// <summary>
/// Extensiones compartidas de FluentValidation para aplicar las reglas de validación de identificadores (Guid).
/// </summary>
public static class IdValidationExtensions
{
    public const string RequiredIdErrorMessage = "El identificador es obligatorio y no puede estar vacío.";

    public static IRuleBuilderOptions<T, Guid> ApplyRequiredIdRule<T>(this IRuleBuilder<T, Guid> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage(RequiredIdErrorMessage);
    }

    public static IRuleBuilderOptions<T, Guid?> ApplyOptionalIdRule<T>(this IRuleBuilder<T, Guid?> ruleBuilder)
    {
        return ruleBuilder
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage(RequiredIdErrorMessage);
    }
}
