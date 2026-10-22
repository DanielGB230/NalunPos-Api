namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;

public class FakeSystemExecutionScope : ISystemExecutionScope
{
    private string? _workerName;
    private HashSet<Type>? _allowedTypes;

    public bool IsActive => _workerName != null;
    public string? WorkerName => _workerName;

    public bool IsCommandAllowed(Type requestType)
    {
        return IsActive && (_allowedTypes?.Contains(requestType) ?? false);
    }

    public IDisposable BeginScope(string workerName, IEnumerable<Type> allowedRequestTypes)
    {
        _workerName = workerName;
        _allowedTypes = new HashSet<Type>(allowedRequestTypes);
        return new DisposableAction(() =>
        {
            _workerName = null;
            _allowedTypes = null;
        });
    }

    private sealed class DisposableAction : IDisposable
    {
        private readonly Action _action;
        public DisposableAction(Action action) => _action = action;
        public void Dispose() => _action();
    }
}
