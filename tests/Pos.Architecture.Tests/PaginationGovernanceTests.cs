using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentValidation;
using Pos.Application.Common.Validation;
using Pos.Architecture.Tests.Support;
using Xunit;

namespace Pos.Architecture.Tests;

public class PaginationGovernanceTests
{
    [Fact]
    public void PaginatedQueries_ApplyPaginationRulesByBehavior()
    {
        var paginatedQueryTypes = CqrsTypeCatalog.GetQueries()
            .Where(CqrsTypeCatalog.IsPaginatedQuery)
            .ToList();

        var failures = new List<string>();

        foreach (var queryType in paginatedQueryTypes)
        {
            var validatorType = CqrsTypeCatalog.GetValidatorFor(queryType);

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
            var baseQuery = QueryInstanceFactory.CreateInstance(queryType, 1, 10, out var createErrors);
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
            var pageNumberZeroQuery = QueryInstanceFactory.CreateInstance(queryType, 0, 10, out _);
            var pageNumberZeroResult = validator.Validate(new ValidationContext<object>(pageNumberZeroQuery));
            if (pageNumberZeroResult.IsValid || !pageNumberZeroResult.Errors.Any(e => e.PropertyName == "PageNumber"))
            {
                failures.Add($"La query '{queryType.Name}' debe producir un error de validación en la propiedad 'PageNumber' cuando PageNumber=0.");
            }

            // 3. PageSize = 0 -> error on PageSize
            var pageSizeZeroQuery = QueryInstanceFactory.CreateInstance(queryType, 1, 0, out _);
            var pageSizeZeroResult = validator.Validate(new ValidationContext<object>(pageSizeZeroQuery));
            if (pageSizeZeroResult.IsValid || !pageSizeZeroResult.Errors.Any(e => e.PropertyName == "PageSize"))
            {
                failures.Add($"La query '{queryType.Name}' debe producir un error de validación en la propiedad 'PageSize' cuando PageSize=0.");
            }

            // 4. PageSize = MaxPageSize + 1 -> error on PageSize
            var pageSizeExceededQuery = QueryInstanceFactory.CreateInstance(queryType, 1, PaginationRules.MaxPageSize + 1, out _);
            var pageSizeExceededResult = validator.Validate(new ValidationContext<object>(pageSizeExceededQuery));
            if (pageSizeExceededResult.IsValid || !pageSizeExceededResult.Errors.Any(e => e.PropertyName == "PageSize"))
            {
                failures.Add($"La query '{queryType.Name}' debe producir un error de validación en la propiedad 'PageSize' cuando PageSize={PaginationRules.MaxPageSize + 1}.");
            }

            // 5. PageSize = MaxPageSize -> valid
            var pageSizeMaxQuery = QueryInstanceFactory.CreateInstance(queryType, 1, PaginationRules.MaxPageSize, out _);
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

            var textWithoutComments = SourceCode.StripCommentsAndStrings(rawText);

            // 1. MaxPageSize check: only allowed inside Common/Validation directory
            if (textWithoutComments.Contains("MaxPageSize"))
            {
                var fileDir = Path.GetDirectoryName(filePath);
                if (!string.Equals(fileDir, commonValidationDir, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"El literal o símbolo 'MaxPageSize' se encontró en '{relativePath}'. Solo puede aparecer dentro de 'Pos.Application/Common/Validation'.");
                }
            }

            // 2. Math.Min/Math.Max/Math.Clamp operating on PageSize or PageNumber (Balanced parentheses scan)
            if (HasMathOperationOnPagination(textWithoutComments, out var mathCall))
            {
                failures.Add($"Se encontró una operación de Math sobre la paginación en '{relativePath}': {mathCall}. La paginación debe ser validada por FluentValidation y PaginationRules.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static bool HasMathOperationOnPagination(string textWithoutComments, out string matchedExpression)
    {
        matchedExpression = "";
        var mathRegex = new Regex(@"Math\.(Min|Max|Clamp)\s*\(");
        var matches = mathRegex.Matches(textWithoutComments);

        foreach (Match m in matches)
        {
            int startIndex = m.Index;
            int openParenIndex = textWithoutComments.IndexOf('(', startIndex);
            if (openParenIndex < 0) continue;

            int depth = 1;
            int currIndex = openParenIndex + 1;
            while (currIndex < textWithoutComments.Length && depth > 0)
            {
                char c = textWithoutComments[currIndex];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                currIndex++;
            }

            if (depth == 0)
            {
                string fullCall = textWithoutComments.Substring(startIndex, currIndex - startIndex);
                if (fullCall.Contains("PageSize") || fullCall.Contains("PageNumber"))
                {
                    matchedExpression = fullCall;
                    return true;
                }
            }
        }

        return false;
    }
}
