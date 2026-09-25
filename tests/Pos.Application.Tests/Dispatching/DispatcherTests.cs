using Pos.Application.Common.Dispatching;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Xunit;

namespace Pos.Application.Tests.Dispatching;

public class DispatcherTests
{
    public record TestCommand(string Input) : ICommand<Result<string>>;

    public class TestCommandHandler : ICommandHandler<TestCommand, Result<string>>
    {
        public Task<Result<string>> HandleAsync(TestCommand request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Ok($"Processed: {request.Input}"));
        }
    }

    public record TestQuery(int Number) : IQuery<int>;

    public class TestQueryHandler : IQueryHandler<TestQuery, int>
    {
        public Task<int> HandleAsync(TestQuery request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request.Number * 2);
        }
    }

    public record TestDomainEvent(Guid Id, DateTime OccurredOnUtc) : IDomainEvent;

    public class TestDomainEventObserver : IDomainEventHandler<TestDomainEvent>
    {
        public static int ExecutedCount { get; set; }

        public Task HandleAsync(TestDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            ExecutedCount++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SendAsync_Command_ShouldResolveHandlerAndReturnResultWithoutDynamic()
    {
        var provider = new FakeServiceProvider();
        provider.Register<ICommandHandler<TestCommand, Result<string>>>(new TestCommandHandler());

        var dispatcher = new Dispatcher(provider);
        var command = new TestCommand("Hello World");

        var result = await dispatcher.SendAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal("Processed: Hello World", result.Value);
    }

    [Fact]
    public async Task QueryAsync_Query_ShouldResolveHandlerAndReturnResultWithoutDynamic()
    {
        var provider = new FakeServiceProvider();
        provider.Register<IQueryHandler<TestQuery, int>>(new TestQueryHandler());

        var dispatcher = new Dispatcher(provider);
        var query = new TestQuery(21);

        var result = await dispatcher.QueryAsync(query);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task PublishAsync_DomainEvent_ShouldInvokeRegisteredHandlers()
    {
        TestDomainEventObserver.ExecutedCount = 0;
        var provider = new FakeServiceProvider();
        provider.Register<IDomainEventHandler<TestDomainEvent>>(new TestDomainEventObserver());

        var dispatcher = new Dispatcher(provider);
        var evt = new TestDomainEvent(Guid.NewGuid(), DateTime.UtcNow);

        await dispatcher.PublishAsync(evt);

        Assert.Equal(1, TestDomainEventObserver.ExecutedCount);
    }

    [Fact]
    public async Task HighConcurrency_SendAsync_ShouldUseCachedInvokersThreadSafely()
    {
        var provider = new FakeServiceProvider();
        provider.Register<ICommandHandler<TestCommand, Result<string>>>(new TestCommandHandler());

        var dispatcher = new Dispatcher(provider);

        var tasks = Enumerable.Range(1, 100).Select(i => Task.Run(async () =>
        {
            var cmd = new TestCommand($"Iter_{i}");
            var res = await dispatcher.SendAsync(cmd);
            Assert.True(res.IsSuccess);
            Assert.Equal($"Processed: Iter_{i}", res.Value);
        }));

        await Task.WhenAll(tasks);
    }

    private sealed class FakeServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();

        public void Register<TService>(object implementation)
        {
            _services[typeof(TService)] = implementation;
        }

        public object? GetService(Type serviceType)
        {
            if (_services.TryGetValue(serviceType, out var service))
            {
                return service;
            }

            if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                var itemType = serviceType.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(itemType);
                var list = (System.Collections.IList)Activator.CreateInstance(listType)!;

                if (_services.TryGetValue(itemType, out var singleService))
                {
                    list.Add(singleService);
                }

                return list;
            }

            return null;
        }
    }
}
