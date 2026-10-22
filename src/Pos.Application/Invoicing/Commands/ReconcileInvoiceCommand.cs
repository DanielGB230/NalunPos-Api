using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Invoicing.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;

using Pos.Domain.Exceptions;

namespace Pos.Application.Invoicing.Commands;

[RequiresTenant]
[HasPermission(Permissions.Invoices.Issue)]
public record ReconcileInvoiceCommand(Guid InvoiceId, int MaxAttempts = 5) : ICommand<Result<InvoiceDto>>;

public class ReconcileInvoiceCommandHandler : ICommandHandler<ReconcileInvoiceCommand, Result<InvoiceDto>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IElectronicInvoicingService _invoicingService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;

    public ReconcileInvoiceCommandHandler(
        IInvoiceRepository invoiceRepository,
        IElectronicInvoicingService invoicingService,
        IUnitOfWork unitOfWork,
        IDispatcher dispatcher,
        TimeProvider? timeProvider = null)
    {
        _invoiceRepository = invoiceRepository ?? throw new ArgumentNullException(nameof(invoiceRepository));
        _invoicingService = invoicingService ?? throw new ArgumentNullException(nameof(invoicingService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Result<InvoiceDto>> HandleAsync(ReconcileInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice == null)
        {
            return Result.Fail<InvoiceDto>(DomainError.NotFound("Invoice.NotFound", $"No se encontró la factura con el ID '{request.InvoiceId}'."));
        }

        if (invoice.Status == InvoiceStatus.RequiresManualIntervention)
        {
            return Result.Fail<InvoiceDto>(DomainError.Conflict(
                "Invoice.ReconciliationExhausted",
                $"La factura con ID '{request.InvoiceId}' se encuentra en estado 'RequiresManualIntervention'."));
        }

        if (invoice.ReconciliationAttempts >= request.MaxAttempts)
        {
            invoice.MarkAsRequiresManualIntervention(
                $"Superó el límite de {request.MaxAttempts} reintentos de reconciliación fiscal sin confirmación del proveedor.",
                _timeProvider);

            _invoiceRepository.Update(invoice);
            var exhaustedEvents = invoice.DomainEvents.ToList();

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyException)
            {
                return Result.Fail<InvoiceDto>(DomainError.Conflict(
                    "Invoice.ConcurrencyConflict",
                    $"Conflicto de concurrencia al actualizar la factura con ID '{request.InvoiceId}' a intervención manual."));
            }

            foreach (var domainEvent in exhaustedEvents)
            {
                await _dispatcher.PublishAsync(domainEvent, cancellationToken);
            }

            return Result.Fail<InvoiceDto>(DomainError.Conflict(
                "Invoice.ReconciliationExhausted",
                $"La factura con ID '{request.InvoiceId}' alcanzó el límite de intentos ({request.MaxAttempts}) y fue marcada para intervención manual."));
        }

        if (invoice.Status != InvoiceStatus.Pending)
        {
            return Result.Fail<InvoiceDto>(DomainError.Conflict("Invoice.AlreadyProcessed", $"La factura con ID '{request.InvoiceId}' ya fue procesada y se encuentra en estado '{invoice.Status}'."));
        }

        var statusResult = await _invoicingService.GetStatusAsync(invoice, cancellationToken);

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
            if (invoice.ReconciliationAttempts >= request.MaxAttempts)
            {
                invoice.MarkAsRequiresManualIntervention(
                    $"Superó el límite de {request.MaxAttempts} reintentos de reconciliación fiscal sin confirmación del proveedor",
                    _timeProvider);
            }
            else
            {
                var sendResult = await _invoicingService.SendInvoiceAsync(invoice, cancellationToken);
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
        }

        _invoiceRepository.Update(invoice);

        var domainEvents = invoice.DomainEvents.ToList();

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return Result.Fail<InvoiceDto>(DomainError.Conflict(
                "Invoice.ConcurrencyConflict",
                $"Conflicto de concurrencia al conciliar la factura con ID '{request.InvoiceId}'."));
        }

        foreach (var domainEvent in domainEvents)
        {
            await _dispatcher.PublishAsync(domainEvent, cancellationToken);
        }

        if (invoice.Status == InvoiceStatus.RequiresManualIntervention)
        {
            return Result.Fail<InvoiceDto>(DomainError.Conflict(
                "Invoice.ReconciliationExhausted",
                $"La factura con ID '{request.InvoiceId}' alcanzó el límite de intentos ({request.MaxAttempts}) y fue marcada para intervención manual."));
        }

        return Result.Ok(InvoiceDto.FromEntity(invoice));
    }
}
