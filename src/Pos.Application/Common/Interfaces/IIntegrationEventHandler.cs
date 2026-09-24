using Pos.Application.IntegrationEvents.Contracts;

namespace Pos.Application.Common.Interfaces;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public interface IIntegrationEventHandler<in TIntegrationEvent> where TIntegrationEvent : IIntegrationEvent
{
    Task HandleAsync(TIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
