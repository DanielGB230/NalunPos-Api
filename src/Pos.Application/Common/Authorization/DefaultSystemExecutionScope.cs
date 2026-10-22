namespace Pos.Application.Common.Authorization;

using Pos.Application.Common.Interfaces;

/// <summary>
/// Implementación por defecto inactiva (Fail-Closed) para ISystemExecutionScope.
/// Permite que la capa de Application resuelva las dependencias del pipeline de autorización
/// sin imponer un acoplamiento directo con la capa de Infrastructure.
/// </summary>
public sealed class DefaultSystemExecutionScope : ISystemExecutionScope
{
    public bool IsActive => false;
    public string? WorkerName => null;

    public bool IsCommandAllowed(Type requestType) => false;

    public IDisposable BeginScope(string workerName, IEnumerable<Type> allowedRequestTypes)
    {
        throw new NotSupportedException(
            "DefaultSystemExecutionScope no admite activación de ámbitos. Registre la implementación de infraestructura.");
    }
}
