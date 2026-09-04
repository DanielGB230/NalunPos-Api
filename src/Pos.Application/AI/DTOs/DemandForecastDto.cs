namespace Pos.Application.AI.DTOs;

public record DemandForecastDto(
    Guid ProductId,
    int DaysAhead,
    int PredictedRequiredQuantity,
    DateTime CalculatedAtUtc
);
