namespace Pos.Application.IntegrationEvents.Contracts.V1;

public record UserStatusChangedIntegrationEventV1(
    Guid Id,
    Guid UserId,
    Guid? TenantId,
    bool IsActive,
    DateTimeOffset OccurredOnUtc
) : IIntegrationEvent;
