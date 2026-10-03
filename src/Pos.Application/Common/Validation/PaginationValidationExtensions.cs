using FluentValidation;

namespace Pos.Application.Common.Validation;

/// <summary>
/// Extensiones compartidas de FluentValidation para aplicar las reglas de paginación
/// en todas las queries de la aplicación.
/// </summary>
public static class PaginationValidationExtensions
{
    public static IRuleBuilderOptions<T, int> ApplyPageNumberRule<T>(this IRuleBuilder<T, int> ruleBuilder)
    {
        return ruleBuilder
            .GreaterThanOrEqualTo(1)
            .WithMessage("El número de página debe ser mayor o igual a 1.");
    }

    public static IRuleBuilderOptions<T, int> ApplyPageSizeRule<T>(this IRuleBuilder<T, int> ruleBuilder)
    {
        return ruleBuilder
            .InclusiveBetween(1, PaginationRules.MaxPageSize)
            .WithMessage($"El tamaño de página debe estar entre 1 y {PaginationRules.MaxPageSize}.");
    }
}
