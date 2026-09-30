using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pos.Application.Common.Interfaces;
using Pos.Application.IntegrationEvents.Contracts;
using Pos.Infrastructure.Multitenancy;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Outbox;

namespace Pos.Api.BackgroundServices;

/// <summary>
/// Background Worker del patrón Transactional Outbox (Sección 10).
/// Procesa periódicamente los registros pendientes en OutboxMessages y los publica a través del IEventBus.
/// Respeta estrictamente el intervalo configurado usando PeriodicTimer de .NET.
/// </summary>
public class OutboxProcessorBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessorBackgroundService> _logger;
    private readonly OutboxSettings _options;

    public OutboxProcessorBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessorBackgroundService> logger,
        IOptions<OutboxSettings> options)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "OutboxProcessorBackgroundService iniciado. Intervalo de polling: {IntervalSeconds}s.",
                _options.PollingIntervalSeconds);
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _options.PollingIntervalSeconds)));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no controlado al procesar mensajes del Outbox.");
            }
        }
    }

    internal async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var tenantSetter = scope.ServiceProvider.GetRequiredService<ITenantSetter>();
        tenantSetter.SetSuperAdmin(true);

        var dbContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        var pendingMessages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null && m.Error == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("No hay mensajes pendientes en el Outbox.");
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Procesando {Count} mensajes del Outbox.", pendingMessages.Count);

        foreach (var message in pendingMessages)
        {
            try
            {
                // Scope 2: Publicación del evento en el bus con su TenantId específico
                using var messageScope = _scopeFactory.CreateScope();
                var msgTenantSetter = messageScope.ServiceProvider.GetRequiredService<ITenantSetter>();
                msgTenantSetter.SetTenantId(message.TenantId);

                var eventBus = messageScope.ServiceProvider.GetRequiredService<IEventBus>();
                Type? eventType = Type.GetType(message.Type);

                if (eventType == null)
                {
                    _logger.LogWarning(
                        "No se pudo resolver el tipo '{MessageType}' para el mensaje Outbox {MessageId}. Marcando como fallido.",
                        message.Type, message.Id);
                    message.MarkAsFailed($"No se pudo resolver el tipo {message.Type}");
                }
                else if (!typeof(IIntegrationEvent).IsAssignableFrom(eventType))
                {
                    _logger.LogWarning(
                        "El tipo '{TypeName}' no implementa IIntegrationEvent para el mensaje Outbox {MessageId}. Marcando como fallido.",
                        eventType.Name, message.Id);
                    message.MarkAsFailed($"El tipo {eventType.Name} no implementa IIntegrationEvent");
                }
                else
                {
                    IIntegrationEvent? integrationEvent = null;
                    try
                    {
                        integrationEvent = JsonSerializer.Deserialize(message.Content, eventType) as IIntegrationEvent;
                    }
                    catch (JsonException jex)
                    {
                        _logger.LogWarning(
                            jex,
                            "Error al deserializar el mensaje Outbox {MessageId} de tipo '{MessageType}'.",
                            message.Id, message.Type);
                        message.MarkAsFailed($"Error de deserialización: {jex.Message}");
                    }

                    if (message.Error == null && integrationEvent == null)
                    {
                        _logger.LogWarning(
                            "Deserialización produjo null para el mensaje Outbox {MessageId} de tipo '{MessageType}'.",
                            message.Id, message.Type);
                        message.MarkAsFailed($"Fallo al deserializar el evento {message.Type}");
                    }
                    else if (message.Error == null && integrationEvent != null)
                    {
                        await eventBus.PublishAsync(integrationEvent, cancellationToken);
                    }
                }

                if (message.Error == null)
                {
                    message.MarkAsProcessed();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo al procesar el mensaje Outbox {MessageId}.", message.Id);
                message.MarkAsFailed(ex.Message);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Outbox: {Processed} procesados, {Failed} fallidos.",
                pendingMessages.Count(m => m.ProcessedOnUtc != null),
                pendingMessages.Count(m => m.Error != null));
        }
    }
}
