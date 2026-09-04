using Pos.Application.AI.Abstractions;

namespace Pos.Infrastructure.AiOrchestration.Adapters;

/// <summary>
/// Adaptador de la Capa Anti-Corrupción (ACL) para predicción de demanda con IA.
/// Simula la estimación calculada sin acoplar el sistema a SDKs de proveedores externos (OpenAI/Anthropic).
/// </summary>
public class DummyDemandForecastAdapter : IDemandForecastCapability
{
    public Task<int> PredictRequiredStockAsync(Guid productId, int daysAhead, CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("El ID del producto no puede ser un GUID vacío.", nameof(productId));
        }

        // Algoritmo simulado determinista basado en el ID y los días hacia adelante
        int baseQuantity = Math.Abs(productId.GetHashCode() % 50) + 10;
        int predictedQuantity = baseQuantity * Math.Max(1, daysAhead / 7);

        return Task.FromResult(predictedQuantity);
    }
}
