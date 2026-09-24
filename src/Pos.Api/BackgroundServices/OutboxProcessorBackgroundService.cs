using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pos.Application.Common.Interfaces;
using Pos.Application.IntegrationEvents.Contracts;
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
                "Iniciando OutboxProcessorBackgroundService con intervalo de {IntervalSeconds} segundos...",
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
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "Error no controlado al procesar mensajes del Outbox.");
                }
            }
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        List<OutboxMessage> pendingMessages;

        // Scope 1: Leer mensajes pendientes (sin tenant específico)
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            pendingMessages = await dbContext.OutboxMessages
                .Where(m => m.ProcessedOnUtc == null)
                .OrderBy(m => m.OccurredOnUtc)
                .Take(20)
                .ToListAsync(cancellationToken);
        }

        if (pendingMessages.Count == 0)
        {
            return;
        }

        foreach (var message in pendingMessages)
        {
            try
            {
                // Scope 2: Procesar cada mensaje de manera aislada con su TenantId
                using var messageScope = _scopeFactory.CreateScope();
                
                var tenantSetter = messageScope.ServiceProvider.GetRequiredService<Pos.Infrastructure.Multitenancy.ITenantSetter>();
                tenantSetter.SetTenantId(message.TenantId);

                var eventBus = messageScope.ServiceProvider.GetRequiredService<IEventBus>();
                var dispatcher = messageScope.ServiceProvider.GetRequiredService<IDispatcher>();

                Type? eventType = Type.GetType(message.Type);
                if (eventType != null && typeof(IIntegrationEvent).IsAssignableFrom(eventType))
                {
                    var integrationEvent = JsonSerializer.Deserialize(message.Content, eventType) as IIntegrationEvent;
                    if (integrationEvent != null)
                    {
                        await eventBus.PublishAsync(integrationEvent, cancellationToken);
                        
                        // Reflection call to Dispatcher since we only know the generic type at runtime
                        var method = dispatcher.GetType().GetMethod("PublishIntegrationEventAsync");
                        var genericMethod = method?.MakeGenericMethod(integrationEvent.GetType());
                        if (genericMethod != null)
                        {
                            var task = (Task)genericMethod.Invoke(dispatcher, new object[] { integrationEvent, cancellationToken })!;
                            await task;
                        }
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

        // Scope 3: Guardar el estado de los mensajes procesados
        using (var updateScope = _scopeFactory.CreateScope())
        {
            var updateDbContext = updateScope.ServiceProvider.GetRequiredService<PosDbContext>();
            updateDbContext.OutboxMessages.UpdateRange(pendingMessages);
            await updateDbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
