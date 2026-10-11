namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Interfaces;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    /// <summary>Contador de llamadas a SaveChangesAsync.</summary>
    public int SaveChangesCount { get; private set; }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        return Task.FromResult(1);
    }
}
