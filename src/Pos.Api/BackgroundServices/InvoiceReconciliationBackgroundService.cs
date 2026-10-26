using Microsoft.Extensions.Options;
using Pos.Api.Options;
using Pos.Application.Common.Interfaces;
using Pos.Application.Invoicing.Commands;
using Pos.Application.Invoicing.Common;
using Pos.Infrastructure.Multitenancy;

namespace Pos.Api.BackgroundServices;

/// <summary>
/// Worker en segundo plano para la orquestación temporal de la reconciliación de facturas pendientes.
/// Responsabilidad única: temporizador y orquestador de scopes limpios por tenant (ADR 0019).
/// Prohibida la lógica de negocio en Pos.Api: la reconciliación se delega al caso de uso ReconcileInvoiceCommand vía IDispatcher.
/// </summary>
public class InvoiceReconciliationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvoiceReconciliationBackgroundService> _logger;
    private readonly InvoiceReconciliationSettings _settings;
    private readonly TimeProvider _timeProvider;

    public InvoiceReconciliationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<InvoiceReconciliationBackgroundService> logger,
        IOptions<InvoiceReconciliationSettings> settings,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Iniciando InvoiceReconciliationBackgroundService con intervalo de {IntervalSeconds}s y umbral de {ThresholdMinutes}m...",
                _settings.IntervalSeconds,
                _settings.ThresholdMinutes);
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _settings.IntervalSeconds)));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ReconcilePendingInvoicesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "Error no controlado durante la reconciliación de facturas en segundo plano.");
                }
            }
        }
    }

    internal async Task ReconcilePendingInvoicesAsync(CancellationToken cancellationToken)
    {
        var thresholdUtc = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-Math.Abs(_settings.ThresholdMinutes));
        IReadOnlyList<PendingInvoiceSummary> pendingInvoices;

        using (var scope = _scopeFactory.CreateScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IPendingInvoicePlatformReader>();
            pendingInvoices = await reader.GetPendingInvoicesDueAsync(
                thresholdUtc,
                _settings.MaxReconciliationAttempts,
                _settings.BatchSize,
                cancellationToken);
        }

        if (pendingInvoices.Count == 0)
        {
            return;
        }

        foreach (var pendingInvoice in pendingInvoices)
        {
            try
            {
                using var tenantScope = _scopeFactory.CreateScope();
                var tenantSetter = tenantScope.ServiceProvider.GetRequiredService<ITenantSetter>();
                tenantSetter.SetTenantId(pendingInvoice.TenantId);

                var systemScope = tenantScope.ServiceProvider.GetRequiredService<ISystemExecutionScope>();
                using var executionScope = systemScope.BeginScope(
                    "InvoiceReconciliationBackgroundService",
                    new[] { typeof(ReconcileInvoiceCommand) });

                var dispatcher = tenantScope.ServiceProvider.GetRequiredService<IDispatcher>();
                var result = await dispatcher.SendAsync(
                    new ReconcileInvoiceCommand(pendingInvoice.Id, _settings.MaxReconciliationAttempts),
                    cancellationToken);

                if (!result.IsSuccess)
                {
                    if (result.Error.Code == InvoiceErrors.ConcurrencyConflict)
                    {
                        if (_logger.IsEnabled(LogLevel.Information))
                        {
                            _logger.LogInformation(
                                "Factura ID {InvoiceId} del Tenant {TenantId} ya fue conciliada concurrentemente por otro proceso.",
                                pendingInvoice.Id,
                                pendingInvoice.TenantId);
                        }
                    }
                    else if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning(
                            "No se pudo reconciliar la factura ID {InvoiceId} del Tenant {TenantId}: {ErrorMessage}",
                            pendingInvoice.Id,
                            pendingInvoice.TenantId,
                            result.Error.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(ex, "Fallo al reconciliar factura ID {InvoiceId} del Tenant {TenantId}", pendingInvoice.Id, pendingInvoice.TenantId);
                }
            }
        }
    }
}
