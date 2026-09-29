using System;

namespace Pos.Application.IntegrationEvents.Contracts.V1;

public record ProductStatusChangedIntegrationEventV1(
    Guid Id,
    Guid ProductId,
    Guid TenantId,
    bool IsActive,
    DateTimeOffset OccurredOnUtc
) : IIntegrationEvent;
