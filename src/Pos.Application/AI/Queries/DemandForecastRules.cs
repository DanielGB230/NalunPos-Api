namespace Pos.Application.AI.Queries;

/// <summary>
/// Reglas constantes para la consulta de proyección de demanda.
/// </summary>
public static class DemandForecastRules
{
    public const int MinDaysAhead = 1;
    public const int MaxDaysAhead = 365;
    public const string DaysAheadErrorMessage = "Los días de proyección deben estar entre 1 y 365.";
}
