using Pos.Application.Tests.Support.Fakes;
using Pos.Application.Common.Interfaces;
using Pos.Application.Invoicing.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Invoicing;

public class ReconcileInvoiceCommandHandlerTests
{
    private readonly FakeInvoiceRepository _invoiceRepository = new();
    private readonly FakeElectronicInvoicingService _invoicingService = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeDispatcher _dispatcher = new();
    private readonly ReconcileInvoiceCommandHandler _handler;

    public ReconcileInvoiceCommandHandlerTests()
    {
        _handler = new ReconcileInvoiceCommandHandler(
            _invoiceRepository,
            _invoicingService,
            _unitOfWork,
            _dispatcher,
            TimeProvider.System);
    }

    [Fact]
    public async Task HandleAsync_WhenInvoiceDoesNotExist_ShouldReturnNotFoundResult()
    {
        var command = new ReconcileInvoiceCommand(Guid.NewGuid());

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Invoice.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenInvoiceAlreadyProcessed_ShouldReturnConflictResult()
    {
        var invoice = Invoice.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            InvoiceDocumentType.Invoice,
            "F001-00000002",
            TaxId.Create("20123456789", "PE"),
            Money.Create(100m, "USD"),
            TimeProvider.System);
        invoice.MarkAsSent(TimeProvider.System);
        invoice.MarkAsAccepted(TimeProvider.System);
        _invoiceRepository.Invoices.Add(invoice);

        var command = new ReconcileInvoiceCommand(invoice.Id);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Invoice.AlreadyProcessed", result.Error.Code);
        Assert.Equal(0, _invoicingService.GetStatusCallCount);
        Assert.Equal(0, _invoicingService.SendInvoiceCallCount);
        Assert.Empty(_dispatcher.PublishedDomainEvents);
    }

    [Fact]
    public async Task HandleAsync_WhenConcurrencyExceptionOccurs_ShouldReturnConflictResultAndPublishZeroEvents()
    {
        var invoice = Invoice.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            InvoiceDocumentType.Invoice,
            "F001-00000009",
            TaxId.Create("20123456789", "PE"),
            Money.Create(150m, "USD"),
            TimeProvider.System);
        _invoiceRepository.Invoices.Add(invoice);

        _invoicingService.ReturnStatus(ElectronicInvoiceProviderStatus.Accepted);
        _unitOfWork.ExceptionToThrowOnSave = new ConcurrencyException("Conflicto de concurrencia simulado en base de datos.");

        var command = new ReconcileInvoiceCommand(invoice.Id);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Invoice.ConcurrencyConflict", result.Error.Code);
        Assert.Empty(_dispatcher.PublishedDomainEvents);
    }

    [Fact]
    public async Task HandleAsync_WhenSuccessful_ShouldPublishDomainEventsExactlyOnce()
    {
        var invoice = Invoice.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            InvoiceDocumentType.Invoice,
            "F001-00000010",
            TaxId.Create("20123456789", "PE"),
            Money.Create(200m, "USD"),
            TimeProvider.System);
        _invoiceRepository.Invoices.Add(invoice);

        _invoicingService.ReturnStatus(ElectronicInvoiceProviderStatus.Accepted);

        var command = new ReconcileInvoiceCommand(invoice.Id);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(_dispatcher.PublishedDomainEvents);
        // Cada evento publicado una sola vez
        Assert.Equal(_dispatcher.PublishedDomainEvents.Count, _dispatcher.PublishedDomainEvents.Distinct().Count());
    }

    [Fact]
    public async Task HandleAsync_WhenProviderReturnsAccepted_ShouldMarkAsAccepted()
    {
        var invoice = Invoice.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            InvoiceDocumentType.Invoice,
            "F001-00000003",
            TaxId.Create("20123456789", "PE"),
            Money.Create(100m, "USD"),
            TimeProvider.System);
        _invoiceRepository.Invoices.Add(invoice);

        _invoicingService.ReturnStatus(ElectronicInvoiceProviderStatus.Accepted);

        var command = new ReconcileInvoiceCommand(invoice.Id);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.Accepted.ToString(), result.Value.StatusName);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenProviderReturnsNotFound_ShouldRetrySendInvoice()
    {
        var invoice = Invoice.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            InvoiceDocumentType.Invoice,
            "F001-00000004",
            TaxId.Create("20123456789", "PE"),
            Money.Create(100m, "USD"),
            TimeProvider.System);
        _invoiceRepository.Invoices.Add(invoice);

        _invoicingService.ReturnStatus(ElectronicInvoiceProviderStatus.NotFound);
        _invoicingService.ReturnSendStatus(ElectronicInvoiceProviderStatus.Accepted);

        var command = new ReconcileInvoiceCommand(invoice.Id);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.Accepted.ToString(), result.Value.StatusName);
        Assert.Equal(1, invoice.ReconciliationAttempts);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenMaxAttemptsIsN_PerformsExactlyNAttemptsBeforeMarkingAsRequiresManualIntervention()
    {
        const int maxAttempts = 3;
        var invoice = Invoice.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            InvoiceDocumentType.Invoice,
            "F001-00000099",
            TaxId.Create("20123456789", "PE"),
            Money.Create(100m, "USD"),
            TimeProvider.System);
        _invoiceRepository.Invoices.Add(invoice);

        _invoicingService.ReturnStatus(ElectronicInvoiceProviderStatus.NotFound);
        _invoicingService.ReturnSendStatus(ElectronicInvoiceProviderStatus.NotFound);

        var command = new ReconcileInvoiceCommand(invoice.Id, maxAttempts);

        // Intentos 1 a N-1: Se envían y la factura permanece Pending
        for (int i = 1; i < maxAttempts; i++)
        {
            var res = await _handler.HandleAsync(command, CancellationToken.None);
            Assert.True(res.IsSuccess);
            Assert.Equal(InvoiceStatus.Pending, invoice.Status);
            Assert.Equal(i, invoice.ReconciliationAttempts);
            Assert.Equal(i, _invoicingService.SendInvoiceCallCount);
        }

        // Intento N: Se ejecuta el N-ésimo envío y pasa a RequiresManualIntervention
        await _handler.HandleAsync(command, CancellationToken.None);
        Assert.Equal(maxAttempts, invoice.ReconciliationAttempts);
        Assert.Equal(maxAttempts, _invoicingService.SendInvoiceCallCount);
        Assert.Equal(InvoiceStatus.RequiresManualIntervention, invoice.Status);
    }
}
