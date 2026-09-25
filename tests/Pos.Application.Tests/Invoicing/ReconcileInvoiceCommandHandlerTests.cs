using Pos.Application.Common.Interfaces;
using Pos.Application.Invoicing.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
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
            _dispatcher);
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
            InvoiceDocumentType.Invoice,
            "F001-00000002",
            TaxId.Create("20123456789", "PE"),
            Money.Create(100m, "USD"));
        invoice.MarkAsSent();
        invoice.MarkAsAccepted();
        _invoiceRepository.Invoices.Add(invoice);

        var command = new ReconcileInvoiceCommand(invoice.Id);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Invoice.AlreadyProcessed", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenProviderReturnsAccepted_ShouldMarkAsAccepted()
    {
        var invoice = Invoice.Create(
            Guid.NewGuid(),
            InvoiceDocumentType.Invoice,
            "F001-00000003",
            TaxId.Create("20123456789", "PE"),
            Money.Create(100m, "USD"));
        _invoiceRepository.Invoices.Add(invoice);

        _invoicingService.StatusToReturn = ElectronicInvoiceProviderStatus.Accepted;

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
            InvoiceDocumentType.Invoice,
            "F001-00000004",
            TaxId.Create("20123456789", "PE"),
            Money.Create(100m, "USD"));
        _invoiceRepository.Invoices.Add(invoice);

        _invoicingService.StatusToReturn = ElectronicInvoiceProviderStatus.NotFound;
        _invoicingService.SendStatusToReturn = ElectronicInvoiceProviderStatus.Accepted;

        var command = new ReconcileInvoiceCommand(invoice.Id);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.Accepted.ToString(), result.Value.StatusName);
        Assert.Equal(1, invoice.ReconciliationAttempts);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    private sealed class FakeInvoiceRepository : IInvoiceRepository
    {
        public List<Invoice> Invoices { get; } = [];

        public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Invoices.FirstOrDefault(i => i.Id == id));
        public Task<Invoice?> GetBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default) => Task.FromResult<Invoice?>(null);
        public Task<Invoice?> GetByDocumentNumberAsync(string documentNumber, CancellationToken cancellationToken = default) => Task.FromResult(Invoices.FirstOrDefault(i => i.DocumentNumber.Equals(documentNumber, StringComparison.OrdinalIgnoreCase)));
        public Task<IReadOnlyList<Invoice>> GetPendingInvoicesOlderThanAsync(DateTime thresholdUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Invoice>>(Invoices.Where(i => i.Status == InvoiceStatus.Pending && i.IssueDateUtc <= thresholdUtc).ToList());
        public Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default) { Invoices.Add(invoice); return Task.CompletedTask; }
        public void Update(Invoice invoice) { }
    }

    private sealed class FakeElectronicInvoicingService : IElectronicInvoicingService
    {
        public ElectronicInvoiceProviderStatus StatusToReturn { get; set; } = ElectronicInvoiceProviderStatus.Accepted;
        public ElectronicInvoiceProviderStatus SendStatusToReturn { get; set; } = ElectronicInvoiceProviderStatus.Accepted;

        public Task<InvoicingServiceResult> GetStatusAsync(Invoice invoice, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new InvoicingServiceResult(StatusToReturn, invoice.DocumentNumber, "HASH", null));
        }

        public Task<InvoicingServiceResult> SendInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new InvoicingServiceResult(SendStatusToReturn, invoice.DocumentNumber, "HASH", null));
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveChangesCount++; return Task.FromResult(1); }
    }

    private sealed class FakeDispatcher : IDispatcher
    {
        public Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SendAsync(ICommand command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task PublishAsync<TDomainEvent>(TDomainEvent domainEvent, CancellationToken cancellationToken = default) where TDomainEvent : IDomainEvent => Task.CompletedTask;
        public Task PublishIntegrationEventAsync<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken cancellationToken = default) where TIntegrationEvent : Pos.Application.IntegrationEvents.Contracts.IIntegrationEvent => Task.CompletedTask;
    }
}
