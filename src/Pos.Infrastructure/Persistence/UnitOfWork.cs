using Microsoft.EntityFrameworkCore;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly PosDbContext _context;

    public UnitOfWork(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Conflicto de concurrencia optimista al guardar cambios en la base de datos.", ex);
        }
    }
}
