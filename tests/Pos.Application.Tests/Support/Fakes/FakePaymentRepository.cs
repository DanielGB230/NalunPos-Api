namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Entities;
using Pos.Domain.Interfaces;

public sealed class FakePaymentRepository : IPaymentRepository
{
    public List<Payment> Payments { get; } = [];
    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Payments.FirstOrDefault(p => p.Id == id));
    public Task<IReadOnlyList<Payment>> GetBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Payment>>(Payments.Where(p => p.SaleId == saleId).ToList());
    public Task AddAsync(Payment payment, CancellationToken cancellationToken = default) { Payments.Add(payment); return Task.CompletedTask; }
    public void Update(Payment payment) { }
}
