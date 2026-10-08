using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Validation;
using Pos.Architecture.Tests.Support;
using Xunit;

namespace Pos.Architecture.Tests;

public class PaginationGovernanceTests
{
    private static readonly Assembly ApplicationAssembly = typeof(Pos.Application.DependencyInjection).Assembly;

    private static bool IsQueryType(Type t) =>
        t.IsClass && !t.IsAbstract &&
        t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>));

    private static bool IsPaginatedQuery(Type queryType)
    {
        var ctor = queryType.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
        if (ctor == null) return false;

        var parameters = ctor.GetParameters();
        bool hasPageNumber = parameters.Any(p => string.Equals(p.Name, "PageNumber", StringComparison.OrdinalIgnoreCase) && p.ParameterType == typeof(int));
        bool hasPageSize = parameters.Any(p => string.Equals(p.Name, "PageSize", StringComparison.OrdinalIgnoreCase) && p.ParameterType == typeof(int));

        return hasPageNumber && hasPageSize;
    }

    private static object CreateQueryInstance(Type queryType, int pageNumber, int pageSize, out List<string> errors)
    {
        errors = new List<string>();
        var ctor = queryType.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
        if (ctor == null)
        {
            errors.Add($"Query '{queryType.Name}' no tiene un constructor público.");
            return null!;
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
            return null!;
        }

        return ctor.Invoke(args);
    }

    [Fact]
    public void PaginatedQueries_ApplyPaginationRulesByBehavior()
    {
        var paginatedQueryTypes = ApplicationAssembly.GetTypes().Where(IsQueryType).Where(IsPaginatedQuery).ToList();
        var validatorTypes = ApplicationAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>)))
            .ToList();

        var failures = new List<string>();

        foreach (var queryType in paginatedQueryTypes)
        {
            var expectedValidatorInterface = typeof(IValidator<>).MakeGenericType(queryType);
            var validatorType = validatorTypes.FirstOrDefault(v => expectedValidatorInterface.IsAssignableFrom(v));

            if (validatorType == null)
            {
                failures.Add($"No se encontró un validador para la query paginada '{queryType.Name}'.");
                continue;
            }

            IValidator validator;
            try
            {
                validator = (IValidator)Activator.CreateInstance(validatorType)!;
            }
            catch (Exception ex)
            {
                failures.Add($"No se pudo instanciar el validador '{validatorType.Name}' de la query '{queryType.Name}' sin dependencias: {ex.Message}");
                continue;
            }

            // 1. Base query with PageNumber=1, PageSize=10 -> valid
            var baseQuery = CreateQueryInstance(queryType, 1, 10, out var createErrors);
            if (createErrors.Count > 0)
            {
                failures.AddRange(createErrors);
                continue;
            }

            var baseContext = new ValidationContext<object>(baseQuery);
            var baseResult = validator.Validate(baseContext);
            if (!baseResult.IsValid)
            {
                failures.Add($"La query base de '{queryType.Name}' con PageNumber=1 y PageSize=10 falló validación: {string.Join("; ", baseResult.Errors.Select(e => e.ErrorMessage))}");
            }

            // 2. PageNumber = 0 -> error on PageNumber
            var pageNumberZeroQuery = CreateQueryInstance(queryType, 0, 10, out _);
            var pageNumberZeroResult = validator.Validate(new ValidationContext<object>(pageNumberZeroQuery));
            if (pageNumberZeroResult.IsValid || !pageNumberZeroResult.Errors.Any(e => e.PropertyName == "PageNumber"))
            {
                failures.Add($"La query '{queryType.Name}' debe producir un error de validación en la propiedad 'PageNumber' cuando PageNumber=0.");
            }

            // 3. PageSize = 0 -> error on PageSize
            var pageSizeZeroQuery = CreateQueryInstance(queryType, 1, 0, out _);
            var pageSizeZeroResult = validator.Validate(new ValidationContext<object>(pageSizeZeroQuery));
            if (pageSizeZeroResult.IsValid || !pageSizeZeroResult.Errors.Any(e => e.PropertyName == "PageSize"))
            {
                failures.Add($"La query '{queryType.Name}' debe producir un error de validación en la propiedad 'PageSize' cuando PageSize=0.");
            }

            // 4. PageSize = MaxPageSize + 1 -> error on PageSize
            var pageSizeExceededQuery = CreateQueryInstance(queryType, 1, PaginationRules.MaxPageSize + 1, out _);
            var pageSizeExceededResult = validator.Validate(new ValidationContext<object>(pageSizeExceededQuery));
            if (pageSizeExceededResult.IsValid || !pageSizeExceededResult.Errors.Any(e => e.PropertyName == "PageSize"))
            {
                failures.Add($"La query '{queryType.Name}' debe producir un error de validación en la propiedad 'PageSize' cuando PageSize={PaginationRules.MaxPageSize + 1}.");
            }

            // 5. PageSize = MaxPageSize -> valid
            var pageSizeMaxQuery = CreateQueryInstance(queryType, 1, PaginationRules.MaxPageSize, out _);
            var pageSizeMaxResult = validator.Validate(new ValidationContext<object>(pageSizeMaxQuery));
            if (!pageSizeMaxResult.IsValid)
            {
                failures.Add($"La query '{queryType.Name}' debe ser válida cuando PageSize={PaginationRules.MaxPageSize}.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void PaginationRules_IsTheOnlyAuthority()
    {
        var applicationDir = SolutionDirectory.PosApplication;
        var apiDir = SolutionDirectory.PosApi;
        var commonValidationDir = Path.Combine(applicationDir, "Common", "Validation");

        var allCsFiles = Directory.GetFiles(applicationDir, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(apiDir, "*.cs", SearchOption.AllDirectories))
            .ToList();

        var failures = new List<string>();

        foreach (var filePath in allCsFiles)
        {
            var relativePath = Path.GetRelativePath(SolutionDirectory.Root, filePath);
            var rawText = File.ReadAllText(filePath);

            // Strip single line comments and block comments
            var textWithoutComments = Regex.Replace(rawText, @"//.*|/\*[\s\S]*?\*/", "");

            // 1. MaxPageSize check: only allowed inside Common/Validation directory
            if (textWithoutComments.Contains("MaxPageSize"))
            {
                var fileDir = Path.GetDirectoryName(filePath);
                if (!string.Equals(fileDir, commonValidationDir, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"El literal o símbolo 'MaxPageSize' se encontró en '{relativePath}'. Solo puede aparecer dentro de 'Pos.Application/Common/Validation'.");
                }
            }

            // 2. Math.Min/Math.Max/Math.Clamp operating on PageSize or PageNumber
            var mathMatch = Regex.Match(textWithoutComments, @"Math\.(Min|Max|Clamp)\s*\([^)]*?(PageSize|PageNumber)[^)]*?\)");
            if (mathMatch.Success)
            {
                failures.Add($"Se encontró una operación Math.{mathMatch.Groups[1].Value} sobre la paginación en '{relativePath}': {mathMatch.Value}. La paginación debe ser validada por FluentValidation y PaginationRules.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
