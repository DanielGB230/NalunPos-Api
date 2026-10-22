namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Contrato de identidad y contexto de ejecución del sistema acotado y declarativo.
/// Reemplaza el bypass inseguro 'HttpContext == null', exigiendo que los workers
/// en segundo plano declaren explícitamente su identidad y los tipos de comandos autorizados.
/// </summary>
public interface ISystemExecutionScope
{
    /// <summary>Indica si un ámbito de sistema se encuentra activo actualmente.</summary>
    bool IsActive { get; }

    /// <summary>Nombre del worker o proceso en segundo plano que activó el ámbito.</summary>
    string? WorkerName { get; }

    /// <summary>Verifica si el tipo de comando o consulta está explícitamente permitido en este ámbito.</summary>
    bool IsCommandAllowed(Type requestType);

    /// <summary>
    /// Inicia un ámbito de ejecución de sistema acotado a los tipos de comandos autorizados.
    /// Al hacer Dispose se restaura el estado previo.
    /// </summary>
    IDisposable BeginScope(string workerName, IEnumerable<Type> allowedRequestTypes);
}
