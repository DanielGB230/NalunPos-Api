namespace Pos.Application.IntegrationEvents.Contracts;

public interface IIntegrationEvent
{
    Guid Id { get; }
    DateTimeOffset OccurredOnUtc { get; }
}
