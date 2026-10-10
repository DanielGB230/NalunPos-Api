namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

public sealed class FakeSupplierRepository : ISupplierRepository
{
    public List<Supplier> Suppliers { get; } = [];

    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Suppliers.FirstOrDefault(s => s.Id == id));
    public Task<Supplier?> GetByTaxIdAsync(TaxId taxId, CancellationToken cancellationToken = default) => Task.FromResult(Suppliers.FirstOrDefault(s => s.TaxId.Value == taxId.Value));
    public Task<bool> ExistsByTaxIdAsync(TaxId taxId, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(Suppliers.Any(s => s.TaxId.Value == taxId.Value && (excludeId == null || s.Id != excludeId.Value)));
    public Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default) { Suppliers.Add(supplier); return Task.CompletedTask; }
    public void Update(Supplier supplier) { }
    public void Delete(Supplier supplier) => Suppliers.Remove(supplier);
    public Task<(IReadOnlyList<Supplier> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm = null, bool? isActive = null, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<Supplier>, int)>((Suppliers, Suppliers.Count));
}
