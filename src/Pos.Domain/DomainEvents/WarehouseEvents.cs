using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public record WarehouseCreatedDomainEvent(
    Guid WarehouseId,
    Guid TenantId,
    Guid BranchId,
    string Name,
    bool IsDefault,
    DateTime OccurredOnUtc
) : IDomainEvent;
