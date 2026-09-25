using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;

namespace Pos.Application.Common.Dispatching;

/// <summary>
/// Dispatcher CQRS propio — resolución fuertemente tipada mediante invocadores genéricos en caché.
/// 100% libre de `dynamic` y reflexión en el hot path de ejecución.
/// </summary>
public sealed class Dispatcher : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> _commandInvokerCache = new();
    private static readonly ConcurrentDictionary<Type, object> _queryInvokerCache = new();
    private static readonly ConcurrentDictionary<Type, object> _domainEventPublisherCache = new();
    private static readonly ConcurrentDictionary<Type, object> _integrationEventPublisherCache = new();

    private readonly IServiceProvider _serviceProvider;

    public Dispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc/>
    public Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var commandType = command.GetType();
        var invoker = (CommandInvoker<TResponse>)_commandInvokerCache.GetOrAdd(commandType, static type =>
        {
            var responseType = typeof(TResponse);
            var invokerType = typeof(CommandInvokerImpl<,>).MakeGenericType(type, responseType);
            return Activator.CreateInstance(invokerType)!;
        });

        return invoker.InvokeAsync(_serviceProvider, command, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        await SendAsync<Unit>(command, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var queryType = query.GetType();
        var invoker = (QueryInvoker<TResponse>)_queryInvokerCache.GetOrAdd(queryType, static type =>
        {
            var responseType = typeof(TResponse);
            var invokerType = typeof(QueryInvokerImpl<,>).MakeGenericType(type, responseType);
            return Activator.CreateInstance(invokerType)!;
        });

        return invoker.InvokeAsync(_serviceProvider, query, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        return QueryAsync(query, cancellationToken);
    }

    /// <inheritdoc/>
    public Task PublishAsync<TDomainEvent>(TDomainEvent domainEvent, CancellationToken cancellationToken = default)
        where TDomainEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var eventType = domainEvent.GetType();
        var publisher = (DomainEventPublisher)_domainEventPublisherCache.GetOrAdd(eventType, static type =>
        {
            var publisherType = typeof(DomainEventPublisherImpl<>).MakeGenericType(type);
            return (DomainEventPublisher)Activator.CreateInstance(publisherType)!;
        });

        return publisher.PublishAsync(_serviceProvider, domainEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public Task PublishIntegrationEventAsync<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        where TIntegrationEvent : Pos.Application.IntegrationEvents.Contracts.IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var eventType = integrationEvent.GetType();
        var publisher = (IntegrationEventPublisher)_integrationEventPublisherCache.GetOrAdd(eventType, static type =>
        {
            var publisherType = typeof(IntegrationEventPublisherImpl<>).MakeGenericType(type);
            return (IntegrationEventPublisher)Activator.CreateInstance(publisherType)!;
        });

        return publisher.PublishAsync(_serviceProvider, integrationEvent, cancellationToken);
    }

    #region Strongly-Typed Invoker Abstractions & Implementations

    private abstract class CommandInvoker<TResponse>
    {
        public abstract Task<TResponse> InvokeAsync(IServiceProvider provider, object command, CancellationToken cancellationToken);
    }

    private sealed class CommandInvokerImpl<TCommand, TResponse> : CommandInvoker<TResponse>
        where TCommand : ICommand<TResponse>
    {
        public override Task<TResponse> InvokeAsync(IServiceProvider provider, object command, CancellationToken cancellationToken)
        {
            var handler = provider.GetRequiredService<ICommandHandler<TCommand, TResponse>>();
            return handler.HandleAsync((TCommand)command, cancellationToken);
        }
    }

    private abstract class QueryInvoker<TResponse>
    {
        public abstract Task<TResponse> InvokeAsync(IServiceProvider provider, object query, CancellationToken cancellationToken);
    }

    private sealed class QueryInvokerImpl<TQuery, TResponse> : QueryInvoker<TResponse>
        where TQuery : IQuery<TResponse>
    {
        public override Task<TResponse> InvokeAsync(IServiceProvider provider, object query, CancellationToken cancellationToken)
        {
            var handler = provider.GetRequiredService<IQueryHandler<TQuery, TResponse>>();
            return handler.HandleAsync((TQuery)query, cancellationToken);
        }
    }

    private abstract class DomainEventPublisher
    {
        public abstract Task PublishAsync(IServiceProvider provider, object domainEvent, CancellationToken cancellationToken);
    }

    private sealed class DomainEventPublisherImpl<TDomainEvent> : DomainEventPublisher
        where TDomainEvent : IDomainEvent
    {
        public override async Task PublishAsync(IServiceProvider provider, object domainEvent, CancellationToken cancellationToken)
        {
            var handlers = provider.GetServices<IDomainEventHandler<TDomainEvent>>();
            var typedEvent = (TDomainEvent)domainEvent;
            foreach (var handler in handlers)
            {
                if (handler is not null)
                {
                    await handler.HandleAsync(typedEvent, cancellationToken);
                }
            }
        }
    }

    private abstract class IntegrationEventPublisher
    {
        public abstract Task PublishAsync(IServiceProvider provider, object integrationEvent, CancellationToken cancellationToken);
    }

    private sealed class IntegrationEventPublisherImpl<TIntegrationEvent> : IntegrationEventPublisher
        where TIntegrationEvent : Pos.Application.IntegrationEvents.Contracts.IIntegrationEvent
    {
        public override async Task PublishAsync(IServiceProvider provider, object integrationEvent, CancellationToken cancellationToken)
        {
            var handlers = provider.GetServices<IIntegrationEventHandler<TIntegrationEvent>>();
            var typedEvent = (TIntegrationEvent)integrationEvent;
            foreach (var handler in handlers)
            {
                if (handler is not null)
                {
                    await handler.HandleAsync(typedEvent, cancellationToken);
                }
            }
        }
    }

    #endregion
}
