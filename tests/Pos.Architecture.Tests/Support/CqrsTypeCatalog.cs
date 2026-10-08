using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentValidation;
using Pos.Application.Common.Interfaces;

namespace Pos.Architecture.Tests.Support;

public static class CqrsTypeCatalog
{
    public static readonly Assembly ApplicationAssembly = typeof(Pos.Application.DependencyInjection).Assembly;

    public static List<Type> GetCommands()
    {
        return ApplicationAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract &&
                        t.GetInterfaces().Any(i => (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)) || i == typeof(ICommand)))
            .OrderBy(t => t.Name)
            .ToList();
    }

    public static List<Type> GetQueries()
    {
        return ApplicationAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract &&
                        t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>)))
            .OrderBy(t => t.Name)
            .ToList();
    }

    public static ConstructorInfo? GetPrimaryConstructor(Type type)
    {
        return type.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();
    }

    public static Type? GetValidatorFor(Type messageType)
    {
        var expectedValidatorInterface = typeof(IValidator<>).MakeGenericType(messageType);
        var validatorTypes = ApplicationAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract &&
                        t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>)))
            .ToList();

        var matching = validatorTypes.Where(v => expectedValidatorInterface.IsAssignableFrom(v)).ToList();
        return matching.Count == 1 ? matching.Single() : null;
    }

    public static bool IsExemptQuery(Type queryType)
    {
        var ctor = GetPrimaryConstructor(queryType);
        if (ctor == null) return true;
        var parameters = ctor.GetParameters();
        if (parameters.Length == 0) return true;

        return parameters.All(p => p.ParameterType == typeof(bool) || p.ParameterType == typeof(bool?));
    }

    public static bool IsPaginatedQuery(Type queryType)
    {
        var ctor = GetPrimaryConstructor(queryType);
        if (ctor == null) return false;

        var parameters = ctor.GetParameters();
        bool hasPageNumber = parameters.Any(p => string.Equals(p.Name, "PageNumber", StringComparison.OrdinalIgnoreCase) && p.ParameterType == typeof(int));
        bool hasPageSize = parameters.Any(p => string.Equals(p.Name, "PageSize", StringComparison.OrdinalIgnoreCase) && p.ParameterType == typeof(int));

        return hasPageNumber && hasPageSize;
    }
}
