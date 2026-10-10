namespace Pos.Application.Tests.Support.Fakes;

public sealed class FakeServiceProvider : IServiceProvider
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
