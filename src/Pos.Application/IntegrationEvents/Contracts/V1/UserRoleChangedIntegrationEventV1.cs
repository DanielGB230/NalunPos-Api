namespace Pos.Application.IntegrationEvents.Contracts.V1;

public record UserRoleChangedIntegrationEventV1(
    Guid Id,
    Guid UserId,
    Guid OldRoleId,
    Guid NewRoleId,
    Guid? OldTenantId,
    Guid? NewTenantId,
    DateTimeOffset OccurredOnUtc
) : IIntegrationEvent;
