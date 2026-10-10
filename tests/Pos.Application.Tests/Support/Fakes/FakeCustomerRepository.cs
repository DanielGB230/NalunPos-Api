namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

public sealed class FakeCustomerRepository : ICustomerRepository
{
    public List<Customer> Customers { get; } = [];

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Customers.FirstOrDefault(c => c.Id == id));
    public Task<Customer?> GetByTaxIdAsync(TaxId taxId, CancellationToken cancellationToken = default) => Task.FromResult(Customers.FirstOrDefault(c => c.TaxId.Value == taxId.Value));
    public Task<bool> ExistsByTaxIdAsync(TaxId taxId, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(Customers.Any(c => c.TaxId.Value == taxId.Value && (excludeId == null || c.Id != excludeId.Value)));
    public Task AddAsync(Customer customer, CancellationToken cancellationToken = default) { Customers.Add(customer); return Task.CompletedTask; }
    public void Update(Customer customer) { }
    public void Delete(Customer customer) => Customers.Remove(customer);
    public Task<(IReadOnlyList<Customer> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm, bool? isActive = null, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<Customer>, int)>((Customers, Customers.Count));
}
