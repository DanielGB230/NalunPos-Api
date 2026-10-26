namespace Pos.Application.Common.Authorization;

using Pos.Application.Common.Interfaces;

/// <summary>
/// Implementación con ciclo de vida Scoped para gestionar el contexto de sistema explícito.
/// </summary>
public sealed class SystemExecutionScope : ISystemExecutionScope
{
    private string? _workerName;
    private HashSet<Type>? _allowedRequestTypes;

    public bool IsActive => _workerName != null;
    public string? WorkerName => _workerName;

    public bool IsCommandAllowed(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        return IsActive && (_allowedRequestTypes?.Contains(requestType) ?? false);
    }

    public IDisposable BeginScope(string workerName, IEnumerable<Type> allowedRequestTypes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerName);
        ArgumentNullException.ThrowIfNull(allowedRequestTypes);

        _workerName = workerName;
        _allowedRequestTypes = new HashSet<Type>(allowedRequestTypes);

        return new ScopeDisposable(this);
    }

    private void Reset()
    {
        _workerName = null;
        _allowedRequestTypes = null;
    }

    private sealed class ScopeDisposable : IDisposable
    {
        private readonly SystemExecutionScope _scope;
        private bool _disposed;

        public ScopeDisposable(SystemExecutionScope scope)
        {
            _scope = scope;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _scope.Reset();
                _disposed = true;
            }
        }
    }
}
