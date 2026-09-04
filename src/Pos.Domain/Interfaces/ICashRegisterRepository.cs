using Pos.Domain.Entities;

namespace Pos.Domain.Interfaces;

public interface ICashRegisterRepository
{
    Task<CashRegister?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashRegister>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(CashRegister cashRegister, CancellationToken cancellationToken = default);
    void Update(CashRegister cashRegister);

    Task<CashRegisterSession?> GetSessionByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<CashRegisterSession?> GetActiveSessionByRegisterIdAsync(Guid registerId, CancellationToken cancellationToken = default);
    Task AddSessionAsync(CashRegisterSession session, CancellationToken cancellationToken = default);
    void UpdateSession(CashRegisterSession session);
}
