namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;
using Pos.Application.IntegrationEvents.Contracts;
using Pos.Domain.Common;

public sealed class FakeDispatcher : IDispatcher
{
    /// <summary>Indica si se permite el despacho de comandos en el fake.</summary>
    public bool AllowCommandDispatch { get; set; }
    /// <summary>Comandos despachados capturados por el fake.</summary>
    public List<ICommand> DispatchedCommands { get; } = [];

    public Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        if (!AllowCommandDispatch)
        {
            throw new NotImplementedException("Command dispatch is disabled by default. Set AllowCommandDispatch = true to enable.");
        }

        DispatchedCommands.Add(command);
        return Task.CompletedTask;
    }

    public Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    /// <summary>Eventos de dominio publicados capturados por el fake.</summary>
    public List<IDomainEvent> PublishedDomainEvents { get; } = [];

    public Task PublishAsync<TDomainEvent>(TDomainEvent domainEvent, CancellationToken cancellationToken = default) where TDomainEvent : IDomainEvent
    {
        PublishedDomainEvents.Add(domainEvent);
        return Task.CompletedTask;
    }

    public Task PublishIntegrationEventAsync<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken cancellationToken = default) where TIntegrationEvent : IIntegrationEvent => Task.CompletedTask;
}
