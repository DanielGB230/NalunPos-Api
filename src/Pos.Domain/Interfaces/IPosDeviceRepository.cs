using Pos.Domain.Entities;

namespace Pos.Domain.Interfaces;

public interface IPosDeviceRepository
{
    Task<PosDevice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PosDevice?> GetBySerialNumberAsync(string serialNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PosDevice>> GetByBranchIdAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<bool> ExistsBySerialNumberAsync(string serialNumber, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(PosDevice device, CancellationToken cancellationToken = default);
    void Update(PosDevice device);
}
