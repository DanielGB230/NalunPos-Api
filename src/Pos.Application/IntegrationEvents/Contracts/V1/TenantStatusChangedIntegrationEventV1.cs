using System;

namespace Pos.Application.IntegrationEvents.Contracts.V1;

public record TenantStatusChangedIntegrationEventV1(
    Guid Id,
    Guid TenantId,
    string OldStatus,
    string NewStatus,
    DateTimeOffset OccurredOnUtc
) : IIntegrationEvent;
