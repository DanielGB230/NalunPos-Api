namespace Pos.Application.AI.Abstractions;

/// <summary>
/// Abstracción de la Capa Anti-Corrupción (ACL) para capacidades de predicción de demanda con Inteligencia Artificial.
/// </summary>
public interface IDemandForecastCapability
{
    Task<int> PredictRequiredStockAsync(Guid productId, int daysAhead, CancellationToken cancellationToken = default);
}
