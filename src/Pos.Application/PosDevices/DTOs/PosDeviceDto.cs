using Pos.Domain.Entities;

namespace Pos.Application.PosDevices.DTOs;

public record PosDeviceDto(
    Guid Id,
    Guid BranchId,
    string Name,
    string SerialNumber,
    bool IsActive,
    DateTimeOffset? LastPingUtc,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
)
{
    public static PosDeviceDto FromEntity(PosDevice device)
    {
        return new PosDeviceDto(
            device.Id,
            device.BranchId,
            device.Name,
            device.SerialNumber,
            device.IsActive,
            device.LastPingUtc,
            device.CreatedAtUtc,
            device.UpdatedAtUtc
        );
    }
}
