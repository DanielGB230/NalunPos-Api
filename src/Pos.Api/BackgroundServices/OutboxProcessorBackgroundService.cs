using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pos.Api.Options;
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
    private readonly TimeProvider _timeProvider;

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

    private readonly string _instanceId = $"{Environment.MachineName}_{Guid.NewGuid():N}";

    internal async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var tenantSetter = scope.ServiceProvider.GetRequiredService<ITenantSetter>();
        tenantSetter.SetSuperAdmin(true);

        var dbContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();
        var now = _timeProvider.GetUtcNow();
        var leaseDuration = TimeSpan.FromSeconds(_options.LeaseDurationSeconds);
        var leaseUntil = now.Add(leaseDuration);

        List<OutboxMessage> claimedMessages;

        if (dbContext.Database.IsRelational())
        {
            var rawSql = @"
                WITH cte AS (
                    SELECT TOP ({0}) *
                    FROM [dbo].[OutboxMessages] WITH (UPDLOCK, READPAST)
                    WHERE [ProcessedOnUtc] IS NULL
                      AND [IsDeadLetter] = 0
                      AND ([NextRetryAtUtc] IS NULL OR [NextRetryAtUtc] <= {1})
                      AND ([LockedUntilUtc] IS NULL OR [LockedUntilUtc] <= {1})
                    ORDER BY [OccurredOnUtc]
                )
                UPDATE cte
                SET [LockedUntilUtc] = {2}, [LockedBy] = {3}
                OUTPUT INSERTED.*;";

            claimedMessages = await dbContext.OutboxMessages
                .FromSqlRaw(rawSql, _options.BatchSize, now, leaseUntil, _instanceId)
                .AsTracking()
                .ToListAsync(cancellationToken);
        }
        else
        {
            claimedMessages = await dbContext.OutboxMessages
                .Where(m => m.ProcessedOnUtc == null &&
                            !m.IsDeadLetter &&
                            (m.NextRetryAtUtc == null || m.NextRetryAtUtc <= now) &&
                            (m.LockedUntilUtc == null || m.LockedUntilUtc <= now))
                .OrderBy(m => m.OccurredOnUtc)
                .Take(_options.BatchSize)
                .ToListAsync(cancellationToken);

            foreach (var msg in claimedMessages)
            {
                msg.AcquireLease(_instanceId, leaseDuration, _timeProvider);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (claimedMessages.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("No hay mensajes pendientes disponibles para reclamar en el Outbox.");
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Instancia {InstanceId} procesando {Count} mensajes del Outbox con lease hasta {LeaseUntil}.",
                _instanceId, claimedMessages.Count, leaseUntil);

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
                    HandleMessageFailure(message, $"No se pudo resolver el tipo {message.Type}");
                }
                else if (!typeof(IIntegrationEvent).IsAssignableFrom(eventType))
                {
                    HandleMessageFailure(message, $"El tipo {eventType.Name} no implementa IIntegrationEvent");
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
                        HandleMessageFailure(message, $"Error de deserialización: {jex.Message}");
                    }

                    if (integrationEvent == null && !message.IsDeadLetter && message.NextRetryAtUtc == null)
                    {
                        HandleMessageFailure(message, $"Fallo al deserializar el evento {message.Type}");
                    }
                    else if (integrationEvent != null)
                    {
                        await eventBus.PublishAsync(integrationEvent, cancellationToken);
                        await dispatcher.PublishIntegrationEventAsync(integrationEvent, cancellationToken);
                        message.MarkAsProcessed(_timeProvider);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo al procesar el mensaje Outbox {MessageId}.", message.Id);
                HandleMessageFailure(message, ex.Message);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Outbox: {Processed} procesados, {Retrying} reintentando, {DeadLetter} en dead-letter.",
                claimedMessages.Count(m => m.ProcessedOnUtc != null),
                claimedMessages.Count(m => !m.IsDeadLetter && m.ProcessedOnUtc == null),
                claimedMessages.Count(m => m.IsDeadLetter));
        }
    }

    private void HandleMessageFailure(OutboxMessage message, string errorMessage)
    {
        var delay = TimeSpan.FromSeconds(Math.Pow(2, message.RetryCount) * _options.BaseRetryIntervalSeconds);
        message.RecordFailure(errorMessage, _options.MaxRetries, delay, _timeProvider);

        if (message.IsDeadLetter)
        {
            _logger.LogError(
                "Mensaje Outbox {MessageId} ha alcanzado el límite de {MaxRetries} reintentos y fue enviado a Dead-Letter. Error: {Error}",
                message.Id, _options.MaxRetries, errorMessage);
        }
        else
        {
            _logger.LogWarning(
                "Mensaje Outbox {MessageId} falló (intento {Attempt}/{MaxRetries}). Siguiente reintento programado para {NextRetryAtUtc}. Error: {Error}",
                message.Id, message.RetryCount, _options.MaxRetries, message.NextRetryAtUtc, errorMessage);
        }
    }
}
