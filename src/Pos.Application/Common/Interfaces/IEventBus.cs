using Pos.Application.IntegrationEvents.Contracts;

namespace Pos.Application.Common.Interfaces;

public interface IEventBus
{
    Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
