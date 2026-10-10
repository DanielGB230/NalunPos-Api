namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Entities;
using Pos.Domain.Interfaces;

public sealed class FakePosDeviceRepository : IPosDeviceRepository
{
    public List<PosDevice> Devices { get; } = [];

    public Task<PosDevice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Devices.FirstOrDefault(d => d.Id == id));
    public Task<PosDevice?> GetBySerialNumberAsync(string serialNumber, CancellationToken cancellationToken = default) => Task.FromResult(Devices.FirstOrDefault(d => d.SerialNumber.Equals(serialNumber, StringComparison.OrdinalIgnoreCase)));
    public Task<bool> ExistsBySerialNumberAsync(string serialNumber, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(Devices.Any(d => d.SerialNumber.Equals(serialNumber, StringComparison.OrdinalIgnoreCase) && d.Id != excludeId));
    public Task AddAsync(PosDevice device, CancellationToken cancellationToken = default) { Devices.Add(device); return Task.CompletedTask; }
    public void Update(PosDevice device) { }
    public Task<IReadOnlyList<PosDevice>> GetByBranchIdAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PosDevice>>(Devices.Where(d => d.BranchId == branchId).ToList());
}
