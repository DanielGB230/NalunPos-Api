using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Invoicing.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;

namespace Pos.Application.Invoicing.Commands;

[HasPermission(Permissions.Invoices.Issue)]
public record ReconcileInvoiceCommand(Guid InvoiceId) : ICommand<Result<InvoiceDto>>;

[HasPermission(Permissions.Invoices.Issue)]
public class ReconcileInvoiceCommandHandler : ICommandHandler<ReconcileInvoiceCommand, Result<InvoiceDto>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IElectronicInvoicingService _invoicingService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDispatcher _dispatcher;

    public ReconcileInvoiceCommandHandler(
        IInvoiceRepository invoiceRepository,
        IElectronicInvoicingService invoicingService,
        IUnitOfWork unitOfWork,
        IDispatcher dispatcher)
    {
        _invoiceRepository = invoiceRepository ?? throw new ArgumentNullException(nameof(invoiceRepository));
        _invoicingService = invoicingService ?? throw new ArgumentNullException(nameof(invoicingService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<Result<InvoiceDto>> HandleAsync(ReconcileInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice == null)
        {
            return Result.Fail<InvoiceDto>(DomainError.NotFound("Invoice.NotFound", $"No se encontró la factura con el ID '{request.InvoiceId}'."));
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

        _invoiceRepository.Update(invoice);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Omitir silenciosamente en conflicto de concurrencia o cambio paralelo
        }

        foreach (var domainEvent in invoice.DomainEvents)
        {
            await _dispatcher.PublishAsync(domainEvent, cancellationToken);
        }
        invoice.ClearDomainEvents();

        return Result.Ok(InvoiceDto.FromEntity(invoice));
    }
}
