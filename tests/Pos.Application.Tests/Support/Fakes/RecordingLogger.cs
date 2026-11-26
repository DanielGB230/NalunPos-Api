namespace Pos.Application.Tests.Support.Fakes;

using Microsoft.Extensions.Logging;

/// <summary>
/// Logger de pruebas que captura las entradas registradas para poder afirmar sobre ellas.
/// </summary>
public sealed class RecordingLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new LogEntry(logLevel, exception, formatter(state, exception)));
    }

    public sealed record LogEntry(LogLevel Level, Exception? Exception, string Message);
}
