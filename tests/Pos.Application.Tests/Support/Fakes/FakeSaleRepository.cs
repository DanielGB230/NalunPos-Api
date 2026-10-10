namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Entities;
using Pos.Domain.Interfaces;

public sealed class FakeSaleRepository : ISaleRepository
{
    public List<Sale> Sales { get; } = [];

    public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Sales.FirstOrDefault(s => s.Id == id));
    public Task<Sale?> GetByReceiptNumberAsync(string receiptNumber, CancellationToken cancellationToken = default) => Task.FromResult(Sales.FirstOrDefault(s => s.ReceiptNumber.Equals(receiptNumber, StringComparison.OrdinalIgnoreCase)));
    public Task AddAsync(Sale sale, CancellationToken cancellationToken = default) { Sales.Add(sale); return Task.CompletedTask; }
    public void Update(Sale sale) { }
    public Task<(IReadOnlyList<Sale> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Guid? sessionId = null, Guid? customerId = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<Sale>, int)>((Sales.AsReadOnly(), Sales.Count));
}
