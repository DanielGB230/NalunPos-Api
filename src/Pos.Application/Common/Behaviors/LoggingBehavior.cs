using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Pos.Application.Common.Interfaces;

namespace Pos.Application.Common.Behaviors;

/// <summary>
/// Behavior CQRS que registra logs de entrada, salida y duración para cada Command.
/// </summary>
public class LoggingBehavior<TCommand, TResponse> : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private static readonly string CommandName = typeof(TCommand).Name;
    private readonly ICommandHandler<TCommand, TResponse> _inner;
    private readonly ILogger<LoggingBehavior<TCommand, TResponse>> _logger;

    public LoggingBehavior(
        ICommandHandler<TCommand, TResponse> inner,
        ILogger<LoggingBehavior<TCommand, TResponse>> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Iniciando ejecución de Command {CommandName}", CommandName);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await _inner.HandleAsync(command, cancellationToken);
            stopwatch.Stop();

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Command {CommandName} ejecutado exitosamente en {ElapsedMilliseconds} ms",
                    CommandName, stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Error durante la ejecución de Command {CommandName} tras {ElapsedMilliseconds} ms",
                    CommandName, stopwatch.ElapsedMilliseconds);
            }
            throw;
        }
    }
}
