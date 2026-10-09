namespace Pos.Architecture.Tests.Support;

public sealed record QueryInstanceResult(object? Instance, IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0 && Instance != null;
}

public static class QueryInstanceFactory
{
    public static QueryInstanceResult CreateInstance(Type queryType, int pageNumber, int pageSize)
    {
        var errors = new List<string>();
        var ctor = CqrsTypeCatalog.GetPrimaryConstructor(queryType);
        if (ctor == null)
        {
            errors.Add($"Query '{queryType.Name}' no tiene un constructor público.");
            return new QueryInstanceResult(null, errors);
        }

        var parameters = ctor.GetParameters();
        var args = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (string.Equals(p.Name, "PageNumber", StringComparison.OrdinalIgnoreCase) && p.ParameterType == typeof(int))
            {
                args[i] = pageNumber;
            }
            else if (string.Equals(p.Name, "PageSize", StringComparison.OrdinalIgnoreCase) && p.ParameterType == typeof(int))
            {
                args[i] = pageSize;
            }
            else if (p.HasDefaultValue)
            {
                args[i] = p.DefaultValue;
            }
            else if (p.ParameterType == typeof(Guid))
            {
                args[i] = Guid.NewGuid();
            }
            else if (p.ParameterType == typeof(Guid?))
            {
                args[i] = (Guid?)null;
            }
            else if (p.ParameterType == typeof(bool))
            {
                args[i] = false;
            }
            else if (p.ParameterType == typeof(bool?))
            {
                args[i] = (bool?)null;
            }
            else if (p.ParameterType == typeof(string))
            {
                args[i] = null;
            }
            else
            {
                errors.Add($"El parámetro '{p.Name}' de tipo sin default '{p.ParameterType.FullName}' en '{queryType.Name}' no tiene conversor asignado.");
            }
        }

        if (errors.Count > 0)
        {
            return new QueryInstanceResult(null, errors);
        }

        var instance = ctor.Invoke(args);
        return new QueryInstanceResult(instance, errors);
    }
}
