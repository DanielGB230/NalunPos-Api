using Pos.Application.Common.Interfaces;
using Pos.Application.Invoicing.Queries;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Invoicing;

public class GetInvoiceByIdQueryHandlerTests
{
    private readonly FakeInvoiceRepository _invoiceRepository = new();

    [Fact]
    public async Task HandleAsync_WhenInvoiceExists_ShouldReturnSuccessResult()
    {
        // Arrange
        var invoice = Invoice.Create(
            Guid.NewGuid(),
            InvoiceDocumentType.Invoice,
            "F001-00000001",
            TaxId.Create("20123456789", "PE"),
            Money.Create(118m, "USD")
        );
        _invoiceRepository.Invoices.Add(invoice);

        var query = new GetInvoiceByIdQuery(invoice.Id);
        var handler = new GetInvoiceByIdQueryHandler(_invoiceRepository);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("F001-00000001", result.Value.DocumentNumber);
    }

    [Fact]
    public async Task HandleAsync_WhenInvoiceDoesNotExist_ShouldReturnNotFoundResult()
    {
        // Arrange
        var query = new GetInvoiceByIdQuery(Guid.NewGuid());
        var handler = new GetInvoiceByIdQueryHandler(_invoiceRepository);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Invoice.NotFound", result.Error.Code);
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
        public Task<(IReadOnlyList<Invoice> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? invoiceNumber, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<Invoice>, int)>((Invoices, Invoices.Count));
    }
}
