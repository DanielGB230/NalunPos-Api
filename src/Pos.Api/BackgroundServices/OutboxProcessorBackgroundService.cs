using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Application.IntegrationEvents.Contracts;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Api.BackgroundServices;

/// <summary>
/// Background Worker del patrón Transactional Outbox (Sección 10).
/// Procesa periódicamente los registros pendientes en OutboxMessages y los publica a través del IEventBus.
/// </summary>
public class OutboxProcessorBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessorBackgroundService> _logger;

    public OutboxProcessorBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessorBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Iniciando OutboxProcessorBackgroundService...");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "Error no controlado al procesar mensajes del Outbox.");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var pendingMessages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return;
        }

        foreach (var message in pendingMessages)
        {
            try
            {
                Type? eventType = Type.GetType(message.Type);
                if (eventType != null && typeof(IIntegrationEvent).IsAssignableFrom(eventType))
                {
                    var integrationEvent = JsonSerializer.Deserialize(message.Content, eventType) as IIntegrationEvent;
                    if (integrationEvent != null)
                    {
                        await eventBus.PublishAsync(integrationEvent, cancellationToken);
                    }
                }

                message.MarkAsProcessed();
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(ex, "Fallo al procesar el mensaje Outbox ID {MessageId}", message.Id);
                }

                message.MarkAsFailed(ex.Message);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
