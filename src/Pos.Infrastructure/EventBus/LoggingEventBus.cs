using Microsoft.Extensions.Logging;
using Pos.Application.Common.Interfaces;
using Pos.Application.IntegrationEvents.Contracts;

namespace Pos.Infrastructure.EventBus;

/// <summary>
/// Implementación honesta en proceso del Event Bus mediante logging estructurado de infraestructura.
/// Registra la publicación de eventos de integración sin depender de brokers externos.
/// </summary>
public class LoggingEventBus : IEventBus
{
    private readonly ILogger<LoggingEventBus> _logger;

    public LoggingEventBus(ILogger<LoggingEventBus> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "[LoggingEventBus] Publicando evento {EventType} (ID: {EventId}) a las {OccurredOnUtc}",
                integrationEvent.GetType().Name,
                integrationEvent.Id,
                integrationEvent.OccurredOnUtc);
        }

        return Task.CompletedTask;
    }
}
