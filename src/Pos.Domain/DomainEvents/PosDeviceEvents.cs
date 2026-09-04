using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public record PosDeviceRegisteredDomainEvent(Guid DeviceId, Guid BranchId, string Name, string SerialNumber, DateTime OccurredOnUtc) : IDomainEvent;

public record PosDevicePingedDomainEvent(Guid DeviceId, DateTimeOffset LastPingUtc, DateTime OccurredOnUtc) : IDomainEvent;
