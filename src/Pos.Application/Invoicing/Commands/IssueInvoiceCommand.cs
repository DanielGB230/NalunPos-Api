using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.Invoicing.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.Invoicing.Commands;

public record IssueInvoiceCommand(
    Guid SaleId,
    InvoiceDocumentType DocumentType,
    string DocumentNumber,
    string CustomerTaxId,
    string CustomerTaxCountryCode = "PE"
) : ICommand<InvoiceDto>;

public class IssueInvoiceCommandValidator : AbstractValidator<IssueInvoiceCommand>
{
    public IssueInvoiceCommandValidator()
    {
        RuleFor(x => x.SaleId)
            .NotEmpty().WithMessage("El ID de la venta es requerido.");

        RuleFor(x => x.DocumentNumber)
            .NotEmpty().WithMessage("El número de comprobante es requerido.");

        RuleFor(x => x.CustomerTaxId)
            .NotEmpty().WithMessage("El TaxId del cliente es requerido.");
    }
}

public class IssueInvoiceCommandHandler : ICommandHandler<IssueInvoiceCommand, InvoiceDto>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IElectronicInvoicingService _invoicingService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDispatcher _dispatcher;

    public IssueInvoiceCommandHandler(
        IInvoiceRepository invoiceRepository,
        ISaleRepository saleRepository,
        IElectronicInvoicingService invoicingService,
        IUnitOfWork unitOfWork,
        IDispatcher dispatcher)
    {
        _invoiceRepository = invoiceRepository ?? throw new ArgumentNullException(nameof(invoiceRepository));
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _invoicingService = invoicingService ?? throw new ArgumentNullException(nameof(invoicingService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<InvoiceDto> HandleAsync(IssueInvoiceCommand request, CancellationToken cancellationToken)
    {
        var sale = await _saleRepository.GetByIdAsync(request.SaleId, cancellationToken)
            ?? throw new SaleNotFoundException(request.SaleId);

        var existingInvoice = await _invoiceRepository.GetBySaleIdAsync(request.SaleId, cancellationToken);
        if (existingInvoice != null)
        {
            throw new DomainException($"Ya existe un documento de facturación emitido para la venta (ID: {existingInvoice.Id}).");
        }

        var taxIdVo = TaxId.Create(request.CustomerTaxId, request.CustomerTaxCountryCode);
        var invoice = Invoice.Create(
            request.SaleId,
            request.DocumentType,
            request.DocumentNumber,
            taxIdVo,
            sale.Total);

        await _invoiceRepository.AddAsync(invoice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Enviar a servicio de facturación externa mediante ACL
        var sendResult = await _invoicingService.SendInvoiceAsync(invoice, cancellationToken);
        if (sendResult.IsSuccess)
        {
            invoice.MarkAsSent();
            invoice.MarkAsAccepted();
        }
        else
        {
            invoice.MarkAsRejected();
        }

        _invoiceRepository.Update(invoice);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in invoice.DomainEvents)
        {
            await _dispatcher.PublishAsync(domainEvent, cancellationToken);
        }
        invoice.ClearDomainEvents();

        return InvoiceDto.FromEntity(invoice);
    }
}
