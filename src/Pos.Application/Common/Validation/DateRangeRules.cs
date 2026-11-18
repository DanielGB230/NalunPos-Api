using FluentValidation;

namespace Pos.Application.Common.Validation;

/// <summary>
/// Contrato único de los filtros de rango de fechas (<c>StartDate</c>/<c>EndDate</c>).
/// Igual que <see cref="PaginationRules"/>, centraliza el código estable y la regla de
/// defensa en profundidad aplicada por los validadores de queries.
/// </summary>
public static class DateRangeRules
{
    /// <summary>Código estable de rechazo para un filtro de fecha sin offset explícito.</summary>
    public const string OffsetRequiredCode = "DateRange.OffsetRequired";

    /// <summary>Mensaje determinista del rechazo por falta de offset explícito.</summary>
    public const string OffsetRequiredMessage =
        "El filtro de fecha debe ser un instante UTC con offset explícito (por ejemplo, 2026-01-01T00:00:00Z).";

    /// <summary>
    /// Exige que el valor sea un instante con <c>Kind=Utc</c> o nulo. El borde HTTP ya lo
    /// garantiza; esta regla impide que una query construida internamente lo viole en silencio.
    /// </summary>
    public static IRuleBuilderOptions<T, DateTime?> ApplyUtcInstantRule<T>(
        this IRuleBuilder<T, DateTime?> ruleBuilder)
    {
        ArgumentNullException.ThrowIfNull(ruleBuilder);

        return ruleBuilder
            .Must(BeUtcInstantOrNull)
            .WithMessage(OffsetRequiredMessage)
            .WithErrorCode(OffsetRequiredCode);
    }

    private static bool BeUtcInstantOrNull(DateTime? value) =>
        value is null || value.Value.Kind == DateTimeKind.Utc;
}
