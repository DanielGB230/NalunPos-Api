namespace Pos.Application.Invoicing.EventHandlers;

using Microsoft.Extensions.Logging;
using Pos.Application.Common.Interfaces;
using Pos.Domain.DomainEvents;

/// <summary>
/// Manejador de evento de dominio que emite la alerta fiscal crítica cuando una factura
/// agota sus intentos de reconciliación y requiere intervención manual.
/// Extraído de Pos.Api para mantener las reglas de negocio estrictamente en Application/Domain.
/// </summary>
public sealed class InvoiceReconciliationExhaustedDomainHandler : IDomainEventHandler<InvoiceReconciliationExhaustedDomainEvent>
{
    private readonly ILogger<InvoiceReconciliationExhaustedDomainHandler> _logger;

    public InvoiceReconciliationExhaustedDomainHandler(ILogger<InvoiceReconciliationExhaustedDomainHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task HandleAsync(InvoiceReconciliationExhaustedDomainEvent notification, CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Critical))
        {
            _logger.LogCritical(
                "ALERTA FISCAL: La factura ID {InvoiceId} (Folio: {DocumentNumber}) del Tenant {TenantId} superó el límite de {Attempts} reintentos y fue marcada como RequiresManualIntervention. Razón: {Reason}.",
                notification.InvoiceId,
                notification.DocumentNumber,
                notification.TenantId,
                notification.Attempts,
                notification.Reason);
        }

        return Task.CompletedTask;
    }
}
