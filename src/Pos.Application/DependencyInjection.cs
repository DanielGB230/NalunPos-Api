using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Common.Behaviors;
using Pos.Application.Common.Dispatching;
using Pos.Application.Common.Interfaces;

namespace Pos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // Dispatcher CQRS propio — reemplaza MediatR (ADR 0014: solo software libre/gratuito)
        services.AddScoped<IDispatcher, Dispatcher>();

        // Registrar todos los ICommandHandler<,>, IQueryHandler<,> e IDomainEventHandler<> del assembly via reflexión
        RegisterOpenGenericHandlers(services, assembly, typeof(ICommandHandler<,>));
        RegisterOpenGenericHandlers(services, assembly, typeof(IQueryHandler<,>));
        RegisterOpenGenericHandlers(services, assembly, typeof(IDomainEventHandler<>));
        RegisterOpenGenericHandlers(services, assembly, typeof(IIntegrationEventHandler<>));

        // Aplicar patrón Decorador (ValidationDecorator + LoggingDecorator + AuthorizationBehavior) sobre todos los ICommandHandler<,>
        ApplyCommandHandlerDecorators(services);
        
        // Aplicar patrón Decorador sobre IQueryHandler<,>
        ApplyQueryHandlerDecorators(services);

        // FluentValidation — MIT, permitido por ADR 0014
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }

    private static void RegisterOpenGenericHandlers(
        IServiceCollection services,
        Assembly assembly,
        Type openGenericInterface)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface)
                .Select(i => new { ServiceType = i, ImplementationType = t }));

        foreach (var handler in handlerTypes)
        {
            services.AddScoped(handler.ServiceType, handler.ImplementationType);
        }
    }

    private static void ApplyCommandHandlerDecorators(IServiceCollection services)
    {
        var handlerDescriptors = services
            .Where(s => s.ServiceType.IsGenericType &&
                        s.ServiceType.GetGenericTypeDefinition() == typeof(ICommandHandler<,>))
            .ToList();

        foreach (var descriptor in handlerDescriptors)
        {
            var serviceType = descriptor.ServiceType;
            var commandType = serviceType.GetGenericArguments()[0];
            var responseType = serviceType.GetGenericArguments()[1];

            var validationDecoratorType = typeof(ValidationDecorator<,>).MakeGenericType(commandType, responseType);
            var authorizationBehaviorType = typeof(AuthorizationBehavior<,>).MakeGenericType(commandType, responseType);
            var loggingDecoratorType = typeof(LoggingDecorator<,>).MakeGenericType(commandType, responseType);

            services.Remove(descriptor);

            services.AddScoped(serviceType, provider =>
            {
                object innerHandler = descriptor.ImplementationType != null
                    ? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType)
                    : descriptor.ImplementationFactory!(provider);

                object validatedHandler = ActivatorUtilities.CreateInstance(provider, validationDecoratorType, innerHandler);
                object authorizedHandler = ActivatorUtilities.CreateInstance(provider, authorizationBehaviorType, validatedHandler);
                object loggedHandler = ActivatorUtilities.CreateInstance(provider, loggingDecoratorType, authorizedHandler);

                return loggedHandler;
            });
        }
    }

    private static void ApplyQueryHandlerDecorators(IServiceCollection services)
    {
        var handlerDescriptors = services
            .Where(s => s.ServiceType.IsGenericType &&
                        s.ServiceType.GetGenericTypeDefinition() == typeof(IQueryHandler<,>))
            .ToList();

        foreach (var descriptor in handlerDescriptors)
        {
            var serviceType = descriptor.ServiceType;
            var queryType = serviceType.GetGenericArguments()[0];
            var responseType = serviceType.GetGenericArguments()[1];

            var authorizationBehaviorType = typeof(AuthorizationQueryBehavior<,>).MakeGenericType(queryType, responseType);

            services.Remove(descriptor);

            services.AddScoped(serviceType, provider =>
            {
                object innerHandler = descriptor.ImplementationType != null
                    ? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType)
                    : descriptor.ImplementationFactory!(provider);

                object authorizedHandler = ActivatorUtilities.CreateInstance(provider, authorizationBehaviorType, innerHandler);

                return authorizedHandler;
            });
        }
    }
}
