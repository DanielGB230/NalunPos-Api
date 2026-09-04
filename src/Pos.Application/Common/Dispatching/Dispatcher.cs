using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;

namespace Pos.Application.Common.Dispatching;

/// <summary>
/// Dispatcher CQRS propio — implementación de IDispatcher que resuelve handlers
/// desde el contenedor de DI (IServiceProvider) sin depender de MediatR ni ninguna
/// librería de terceros. Reemplaza a MediatR.Mediator.
/// </summary>
public sealed class Dispatcher : IDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public Dispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResponse));
        dynamic handler = _serviceProvider.GetRequiredService(handlerType);
        return handler.HandleAsync((dynamic)command, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        await SendAsync<Unit>(command, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResponse));
        dynamic handler = _serviceProvider.GetRequiredService(handlerType);
        return handler.HandleAsync((dynamic)query, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        return QueryAsync(query, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task PublishAsync<TDomainEvent>(TDomainEvent domainEvent, CancellationToken cancellationToken = default)
        where TDomainEvent : IDomainEvent
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        var handlers = _serviceProvider.GetServices(handlerType);
        foreach (var handler in handlers)
        {
            if (handler is not null)
            {
                await ((dynamic)handler).HandleAsync((dynamic)domainEvent, cancellationToken);
            }
        }
    }
}
