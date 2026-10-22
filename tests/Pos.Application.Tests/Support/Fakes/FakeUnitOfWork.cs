namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Interfaces;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    /// <summary>Contador de llamadas a SaveChangesAsync.</summary>
    public int SaveChangesCount { get; private set; }
    public Exception? ExceptionToThrowOnSave { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        if (ExceptionToThrowOnSave != null)
        {
            throw ExceptionToThrowOnSave;
        }
        return Task.FromResult(1);
    }
}
