using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Architecture.Tests.Support;
using Xunit;

namespace Pos.Architecture.Tests;

public class CqrsValidationGovernanceTests
{
    private static readonly Assembly ApplicationAssembly = typeof(Pos.Application.DependencyInjection).Assembly;

    public static bool IsCommandType(Type t) =>
        t.IsClass && !t.IsAbstract &&
        t.GetInterfaces().Any(i => (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)) || i == typeof(ICommand));

    public static bool IsQueryType(Type t) =>
        t.IsClass && !t.IsAbstract &&
        t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>));

    public static bool IsExemptQuery(Type queryType)
    {
        var ctor = queryType.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
        if (ctor == null) return true;
        var parameters = ctor.GetParameters();
        if (parameters.Length == 0) return true;

        return parameters.All(p => p.ParameterType == typeof(bool) || p.ParameterType == typeof(bool?));
    }

    [Fact]
    public void EveryCommand_HasItsOwnValidatorInSeparateFile()
    {
        var commandTypes = ApplicationAssembly.GetTypes().Where(IsCommandType).ToList();
        var validatorTypes = ApplicationAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>)))
            .ToList();

        var failures = new List<string>();

        foreach (var commandType in commandTypes)
        {
            var expectedValidatorInterface = typeof(IValidator<>).MakeGenericType(commandType);
            var matchingValidators = validatorTypes.Where(v => expectedValidatorInterface.IsAssignableFrom(v)).ToList();

            if (matchingValidators.Count != 1)
            {
                failures.Add($"Command '{commandType.Name}' debe tener exactamente 1 validador que implemente IValidator<{commandType.Name}>, pero se encontraron {matchingValidators.Count}.");
                continue;
            }

            var validatorType = matchingValidators.Single();
            var expectedValidatorFileName = $"{validatorType.Name}.cs";

            var searchFiles = Directory.GetFiles(SolutionDirectory.PosApplication, "*.cs", SearchOption.AllDirectories);
            var commandFilePath = searchFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == commandType.Name);
            var validatorFilePath = searchFiles.FirstOrDefault(f => Path.GetFileName(f) == expectedValidatorFileName);

            if (commandFilePath == null)
            {
                failures.Add($"No se encontró el archivo fuente para el comando '{commandType.Name}'.");
                continue;
            }

            if (validatorFilePath == null)
            {
                failures.Add($"El validador '{validatorType.Name}' del comando '{commandType.Name}' debe estar en un archivo dedicado '{expectedValidatorFileName}'.");
                continue;
            }

            var commandDir = Path.GetDirectoryName(commandFilePath);
            var validatorDir = Path.GetDirectoryName(validatorFilePath);

            if (!string.Equals(commandDir, validatorDir, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"El validador '{validatorType.Name}' debe estar en la misma carpeta que el comando '{commandType.Name}' ({commandDir}), pero se encontró en ({validatorDir}).");
            }

            var validatorFileContent = File.ReadAllText(validatorFilePath);
            var declaredTypesMatches = Regex.Matches(
                validatorFileContent,
                @"(?ms)^\s*public\s+(sealed\s+)?(class|record|struct|enum|interface)\s+([A-Z]\w+)")
                .Cast<Match>()
                .Select(m => m.Groups[3].Value)
                .ToList();

            if (declaredTypesMatches.Count > 1)
            {
                failures.Add($"El archivo '{expectedValidatorFileName}' declara múltiples tipos ({string.Join(", ", declaredTypesMatches)}). Debe declarar únicamente el validador.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void EveryNonExemptQuery_HasItsOwnValidatorInSeparateFile()
    {
        var queryTypes = ApplicationAssembly.GetTypes().Where(IsQueryType).ToList();
        var validatorTypes = ApplicationAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>)))
            .ToList();

        var failures = new List<string>();

        foreach (var queryType in queryTypes)
        {
            if (IsExemptQuery(queryType))
            {
                continue;
            }

            var expectedValidatorInterface = typeof(IValidator<>).MakeGenericType(queryType);
            var matchingValidators = validatorTypes.Where(v => expectedValidatorInterface.IsAssignableFrom(v)).ToList();

            if (matchingValidators.Count != 1)
            {
                failures.Add($"Query '{queryType.Name}' debe tener exactamente 1 validador que implemente IValidator<{queryType.Name}>, pero se encontraron {matchingValidators.Count}.");
                continue;
            }

            var validatorType = matchingValidators.Single();
            var expectedValidatorFileName = $"{validatorType.Name}.cs";

            var searchFiles = Directory.GetFiles(SolutionDirectory.PosApplication, "*.cs", SearchOption.AllDirectories);
            var queryFilePath = searchFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == queryType.Name);
            var validatorFilePath = searchFiles.FirstOrDefault(f => Path.GetFileName(f) == expectedValidatorFileName);

            if (queryFilePath == null)
            {
                failures.Add($"No se encontró el archivo fuente para la query '{queryType.Name}'.");
                continue;
            }

            if (validatorFilePath == null)
            {
                failures.Add($"El validador '{validatorType.Name}' de la query '{queryType.Name}' debe estar en un archivo dedicado '{expectedValidatorFileName}'.");
                continue;
            }

            var queryDir = Path.GetDirectoryName(queryFilePath);
            var validatorDir = Path.GetDirectoryName(validatorFilePath);

            if (!string.Equals(queryDir, validatorDir, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"El validador '{validatorType.Name}' debe estar en la misma carpeta que la query '{queryType.Name}' ({queryDir}), pero se encontró en ({validatorDir}).");
            }

            var validatorFileContent = File.ReadAllText(validatorFilePath);
            var declaredTypesMatches = Regex.Matches(
                validatorFileContent,
                @"(?ms)^\s*public\s+(sealed\s+)?(class|record|struct|enum|interface)\s+([A-Z]\w+)")
                .Cast<Match>()
                .Select(m => m.Groups[3].Value)
                .ToList();

            if (declaredTypesMatches.Count > 1)
            {
                failures.Add($"El archivo '{expectedValidatorFileName}' declara múltiples tipos ({string.Join(", ", declaredTypesMatches)}). Debe declarar únicamente el validador.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
