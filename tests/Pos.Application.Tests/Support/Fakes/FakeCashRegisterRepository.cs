namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;

public sealed class FakeCashRegisterRepository : ICashRegisterRepository
{
    public List<CashRegister> Registers { get; } = [];
    public List<CashRegisterSession> Sessions { get; } = [];

    public Task<CashRegister?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Registers.FirstOrDefault(r => r.Id == id));
    public Task<IReadOnlyList<CashRegister>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CashRegister>>(Registers);
    public Task AddAsync(CashRegister cashRegister, CancellationToken cancellationToken = default) { Registers.Add(cashRegister); return Task.CompletedTask; }
    public void Update(CashRegister cashRegister) { }
    public Task<CashRegisterSession?> GetSessionByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult(Sessions.FirstOrDefault(s => s.Id == sessionId));
    public Task<CashRegisterSession?> GetActiveSessionByRegisterIdAsync(Guid registerId, CancellationToken cancellationToken = default) => Task.FromResult(Sessions.FirstOrDefault(s => s.CashRegisterId == registerId && s.Status == SessionStatus.Open));
    public Task AddSessionAsync(CashRegisterSession session, CancellationToken cancellationToken = default) { Sessions.Add(session); return Task.CompletedTask; }
    public void UpdateSession(CashRegisterSession session) { }
}
