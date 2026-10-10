namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;

public sealed class FakeInvoiceRepository : IInvoiceRepository
{
    public List<Invoice> Invoices { get; } = [];

    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Invoices.FirstOrDefault(i => i.Id == id));
    public Task<Invoice?> GetBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default) => Task.FromResult(Invoices.FirstOrDefault(i => i.SaleId == saleId));
    public Task<Invoice?> GetByDocumentNumberAsync(string documentNumber, CancellationToken cancellationToken = default) => Task.FromResult(Invoices.FirstOrDefault(i => i.DocumentNumber.Equals(documentNumber, StringComparison.OrdinalIgnoreCase)));
    public Task<IReadOnlyList<Invoice>> GetPendingInvoicesOlderThanAsync(DateTime thresholdUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Invoice>>(Invoices.Where(i => i.Status == InvoiceStatus.Pending && i.IssueDateUtc <= thresholdUtc).ToList());
    public Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default) { Invoices.Add(invoice); return Task.CompletedTask; }
    public void Update(Invoice invoice) { }
    public Task<(IReadOnlyList<Invoice> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Guid? customerId = null, InvoiceStatus? status = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<Invoice>, int)>((Invoices, Invoices.Count));
}
