using Pos.Domain.Common;

namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Dispatcher CQRS propio — reemplaza a MediatR (ISender / IPublisher).
/// Resuelve el handler correcto desde el contenedor de DI y ejecuta el Command, Query o DomainEvent.
/// </summary>
public interface IDispatcher
{
    /// <summary>Envía un Command que retorna TResponse.</summary>
    Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    /// <summary>Envía un Command sin retorno de valor.</summary>
    Task SendAsync(ICommand command, CancellationToken cancellationToken = default);

    /// <summary>Ejecuta una Query que retorna TResponse.</summary>
    Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);

    /// <summary>Sobrecarga conveniente de SendAsync para Queries que retornan TResponse.</summary>
    Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);

    /// <summary>Publica un evento de dominio a todos los IDomainEventHandler registrados.</summary>
    Task PublishAsync<TDomainEvent>(TDomainEvent domainEvent, CancellationToken cancellationToken = default)
        where TDomainEvent : IDomainEvent;

    /// <summary>Publica un evento de integración a todos los IIntegrationEventHandler registrados.</summary>
    Task PublishIntegrationEventAsync<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        where TIntegrationEvent : Pos.Application.IntegrationEvents.Contracts.IIntegrationEvent;
}
