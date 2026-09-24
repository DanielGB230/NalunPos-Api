using Pos.Domain.Entities;

namespace Pos.Domain.Interfaces;

public interface IBranchRepository
{
    Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Branch>> GetAllAsync(bool? isActive = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Branch branch, CancellationToken cancellationToken = default);
    void Update(Branch branch);
}
