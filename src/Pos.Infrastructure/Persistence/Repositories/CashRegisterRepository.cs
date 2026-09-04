using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Repositories;

public class CashRegisterRepository : ICashRegisterRepository
{
    private readonly PosDbContext _context;

    public CashRegisterRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<CashRegister?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CashRegisters.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<CashRegister>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.CashRegisters.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(CashRegister cashRegister, CancellationToken cancellationToken = default)
    {
        await _context.CashRegisters.AddAsync(cashRegister, cancellationToken);
    }

    public void Update(CashRegister cashRegister)
    {
        _context.CashRegisters.Update(cashRegister);
    }

    public async Task<CashRegisterSession?> GetSessionByIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await _context.CashRegisterSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
    }

    public async Task<CashRegisterSession?> GetActiveSessionByRegisterIdAsync(Guid registerId, CancellationToken cancellationToken = default)
    {
        return await _context.CashRegisterSessions
            .FirstOrDefaultAsync(s => s.CashRegisterId == registerId && s.Status == SessionStatus.Open, cancellationToken);
    }

    public async Task AddSessionAsync(CashRegisterSession session, CancellationToken cancellationToken = default)
    {
        await _context.CashRegisterSessions.AddAsync(session, cancellationToken);
    }

    public void UpdateSession(CashRegisterSession session)
    {
        _context.CashRegisterSessions.Update(session);
    }
}
