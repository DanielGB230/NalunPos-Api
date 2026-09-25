using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Multitenancy;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Api.BackgroundServices;

/// <summary>
/// Worker en segundo plano para la reconciliación automática de facturas preliminares (Pending).
/// </summary>
public class InvoiceReconciliationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvoiceReconciliationBackgroundService> _logger;
    private readonly InvoiceReconciliationSettings _settings;

    public InvoiceReconciliationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<InvoiceReconciliationBackgroundService> logger,
        IOptions<InvoiceReconciliationSettings> settings)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
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

    private async Task ReconcilePendingInvoicesAsync(CancellationToken cancellationToken)
    {
        var thresholdUtc = DateTime.UtcNow.AddMinutes(-Math.Abs(_settings.ThresholdMinutes));
        List<Invoice> pendingInvoices;

        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            pendingInvoices = await dbContext.Invoices
                .IgnoreQueryFilters()
                .Where(i => i.Status == InvoiceStatus.Pending && i.IssueDateUtc <= thresholdUtc)
                .OrderBy(i => i.IssueDateUtc)
                .Take(50)
                .ToListAsync(cancellationToken);
        }

        if (pendingInvoices.Count == 0)
        {
            return;
        }

        foreach (var pendingInvoice in pendingInvoices)
        {
            if (pendingInvoice.ReconciliationAttempts >= _settings.MaxReconciliationAttempts)
            {
                if (_logger.IsEnabled(LogLevel.Critical))
                {
                    _logger.LogCritical(
                        "ALERTA FISCAL: La factura ID {InvoiceId} (Folio: {DocumentNumber}) del Tenant {TenantId} superó el límite de reintentos ({MaxAttempts}). Se requiere intervención manual.",
                        pendingInvoice.Id,
                        pendingInvoice.DocumentNumber,
                        pendingInvoice.TenantId,
                        _settings.MaxReconciliationAttempts);
                }
                continue;
            }

            try
            {
                using var tenantScope = _scopeFactory.CreateScope();
                var tenantSetter = tenantScope.ServiceProvider.GetRequiredService<ITenantSetter>();
                tenantSetter.SetTenantId(pendingInvoice.TenantId);

                var invoiceRepo = tenantScope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
                var invoicingService = tenantScope.ServiceProvider.GetRequiredService<IElectronicInvoicingService>();
                var unitOfWork = tenantScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var dispatcher = tenantScope.ServiceProvider.GetRequiredService<IDispatcher>();

                var invoice = await invoiceRepo.GetByIdAsync(pendingInvoice.Id, cancellationToken);
                if (invoice == null || invoice.Status != InvoiceStatus.Pending)
                {
                    continue;
                }

                var statusResult = await invoicingService.GetStatusAsync(invoice, cancellationToken);

                if (statusResult.Status == ElectronicInvoiceProviderStatus.Accepted)
                {
                    invoice.MarkAsSent();
                    invoice.MarkAsAccepted();
                }
                else if (statusResult.Status == ElectronicInvoiceProviderStatus.Rejected)
                {
                    invoice.MarkAsRejected();
                }
                else if (statusResult.Status == ElectronicInvoiceProviderStatus.NotFound)
                {
                    invoice.IncrementReconciliationAttempts();
                    var sendResult = await invoicingService.SendInvoiceAsync(invoice, cancellationToken);
                    if (sendResult.Status == ElectronicInvoiceProviderStatus.Accepted)
                    {
                        invoice.MarkAsSent();
                        invoice.MarkAsAccepted();
                    }
                    else if (sendResult.Status == ElectronicInvoiceProviderStatus.Rejected)
                    {
                        invoice.MarkAsRejected();
                    }
                }

                invoiceRepo.Update(invoice);

                try
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is DbUpdateConcurrencyException || ex is Pos.Domain.Exceptions.ConcurrencyException)
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation(
                            "Concurrencia detectada en la factura {InvoiceId}: ya fue procesada o actualizada por otro hilo/proceso. Omitiendo.",
                            invoice.Id);
                    }
                    continue;
                }

                foreach (var domainEvent in invoice.DomainEvents)
                {
                    await dispatcher.PublishAsync(domainEvent, cancellationToken);
                }
                invoice.ClearDomainEvents();
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(ex, "Fallo al reconciliar factura ID {InvoiceId}", pendingInvoice.Id);
                }
            }
        }
    }
}
