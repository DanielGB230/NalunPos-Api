using Microsoft.Extensions.Logging;
using Pos.Application.Common.Interfaces;
using Pos.Application.IntegrationEvents.Contracts;

namespace Pos.Infrastructure.EventBus.RabbitMq;

/// <summary>
/// Implementación conceptual del Event Bus para RabbitMQ (Sección 11).
/// No instala SDKs pesados ni realiza conexiones reales en esta etapa,
/// simulando la publicación mediante log de infraestructura sin contaminar las capas superiores.
/// </summary>
public class RabbitMqEventBus : IEventBus
{
    private readonly ILogger<RabbitMqEventBus> _logger;

    public RabbitMqEventBus(ILogger<RabbitMqEventBus> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "[EventBus RabbitMQ] Simulando publicación a RabbitMQ del evento {EventType} (ID: {EventId}) a las {OccurredOn}",
                integrationEvent.GetType().Name,
                integrationEvent.Id,
                integrationEvent.OccurredOnUtc);
        }

        return Task.CompletedTask;
    }
}
