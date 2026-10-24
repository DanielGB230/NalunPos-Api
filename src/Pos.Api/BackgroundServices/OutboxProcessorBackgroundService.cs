using System.Text.Json;
using Microsoft.Extensions.Options;
using Pos.Api.Options;
using Pos.Application.Common.Interfaces;
using Pos.Application.IntegrationEvents.Contracts;
using Pos.Infrastructure.Multitenancy;

namespace Pos.Api.BackgroundServices;

/// <summary>
/// Background Worker del patrón Transactional Outbox (Sección 10).
/// Procesa periódicamente los registros pendientes en OutboxMessages y los publica a través del IEventBus.
/// Respeta estrictamente el intervalo configurado usando PeriodicTimer de .NET.
/// El reclamo del lease atómico y persistencia se delegan a IOutboxLeaseClaimer (ADR 0022).
/// </summary>
public class OutboxProcessorBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessorBackgroundService> _logger;
    private readonly OutboxSettings _options;
    private readonly TimeProvider _timeProvider;
    private readonly string _instanceId = $"{Environment.MachineName}_{Guid.NewGuid():N}";

    public OutboxProcessorBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessorBackgroundService> logger,
        IOptions<OutboxSettings> options,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
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
        var claimer = scope.ServiceProvider.GetRequiredService<IOutboxLeaseClaimer>();
        var leaseDuration = TimeSpan.FromSeconds(_options.LeaseDurationSeconds);

        var claimedMessages = await claimer.ClaimMessagesAsync(
            _instanceId,
            _options.BatchSize,
            leaseDuration,
            cancellationToken);

        if (claimedMessages.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("No hay mensajes pendientes disponibles para reclamar en el Outbox.");
            }
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            var now = _timeProvider.GetUtcNow();
            var leaseUntil = now.Add(leaseDuration);
            _logger.LogInformation(
                "Instancia {InstanceId} procesando {Count} mensajes del Outbox con lease hasta {LeaseUntil}.",
                _instanceId, claimedMessages.Count, leaseUntil);
        }

        int processedCount = 0;
        int failedCount = 0;

        foreach (var message in claimedMessages)
        {
            try
            {
                using var messageScope = _scopeFactory.CreateScope();
                var msgTenantSetter = messageScope.ServiceProvider.GetRequiredService<ITenantSetter>();
                msgTenantSetter.SetTenantId(message.TenantId);

                var systemScope = messageScope.ServiceProvider.GetRequiredService<ISystemExecutionScope>();
                using var executionScope = systemScope.BeginScope(
                    "OutboxProcessorBackgroundService",
                    Type.EmptyTypes);

                var eventBus = messageScope.ServiceProvider.GetRequiredService<IEventBus>();
                var dispatcher = messageScope.ServiceProvider.GetRequiredService<IDispatcher>();
                Type? eventType = Type.GetType(message.Type);

                if (eventType == null)
                {
                    await HandleMessageFailureAsync(claimer, message, $"No se pudo resolver el tipo {message.Type}", cancellationToken);
                    failedCount++;
                }
                else if (!typeof(IIntegrationEvent).IsAssignableFrom(eventType))
                {
                    await HandleMessageFailureAsync(claimer, message, $"El tipo {eventType.Name} no implementa IIntegrationEvent", cancellationToken);
                    failedCount++;
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
                        await HandleMessageFailureAsync(claimer, message, $"Error de deserialización: {jex.Message}", cancellationToken);
                        failedCount++;
                    }

                    if (integrationEvent == null && failedCount == 0)
                    {
                        await HandleMessageFailureAsync(claimer, message, $"Fallo al deserializar el evento {message.Type}", cancellationToken);
                        failedCount++;
                    }
                    else if (integrationEvent != null)
                    {
                        await eventBus.PublishAsync(integrationEvent, cancellationToken);
                        await dispatcher.PublishIntegrationEventAsync(integrationEvent, cancellationToken);
                        await claimer.MarkAsProcessedAsync(message.Id, cancellationToken);
                        processedCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo al procesar el mensaje Outbox {MessageId}.", message.Id);
                await HandleMessageFailureAsync(claimer, message, ex.Message, cancellationToken);
                failedCount++;
            }
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Outbox: {Processed} procesados, {Failed} con fallo o reintento.",
                processedCount, failedCount);
        }
    }

    private async Task HandleMessageFailureAsync(
        IOutboxLeaseClaimer claimer,
        ClaimedOutboxMessage message,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Pow(2, message.RetryCount) * _options.BaseRetryIntervalSeconds);
        await claimer.RecordFailureAsync(message.Id, errorMessage, _options.MaxRetries, delay, cancellationToken);

        if ((message.RetryCount + 1) >= _options.MaxRetries)
        {
            _logger.LogError(
                "Mensaje Outbox {MessageId} ha alcanzado el límite de {MaxRetries} reintentos y fue enviado a Dead-Letter. Error: {Error}",
                message.Id, _options.MaxRetries, errorMessage);
        }
        else
        {
            var nextRetryAt = _timeProvider.GetUtcNow().Add(delay);
            _logger.LogWarning(
                "Mensaje Outbox {MessageId} falló (intento {Attempt}/{MaxRetries}). Siguiente reintento programado para {NextRetryAtUtc}. Error: {Error}",
                message.Id, message.RetryCount + 1, _options.MaxRetries, nextRetryAt, errorMessage);
        }
    }
}
