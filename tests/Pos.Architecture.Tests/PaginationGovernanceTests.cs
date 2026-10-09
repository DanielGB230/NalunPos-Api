using FluentValidation;
using Pos.Application.Common.Validation;
using Pos.Architecture.Tests.Support;

namespace Pos.Architecture.Tests;

public class PaginationGovernanceTests
{
    [Fact]
    public void PaginatedQueries_ApplyPaginationRulesByBehavior()
    {
        SourceTreeIndex.EnsureNoSyntaxErrors();
        var paginatedQueries = CqrsTypeCatalog.GetQueries()
            .Where(CqrsTypeCatalog.IsPaginatedQuery)
            .ToList();

        var failures = new List<string>();

        foreach (var queryType in paginatedQueries)
        {
            var validatorType = CqrsTypeCatalog.GetValidatorFor(queryType);
            if (validatorType == null)
            {
                failures.Add($"La query paginada '{queryType.Name}' no tiene un validador registrado en Pos.Application.");
                continue;
            }

            IValidator validator;
            try
            {
                validator = (IValidator)Activator.CreateInstance(validatorType)!;
            }
            catch (Exception ex)
            {
                failures.Add($"La query paginada '{queryType.Name}': no se pudo instanciar el validador '{validatorType.Name}': {ex.Message}");
                continue;
            }

            // 1. Instancia base válida (PageNumber=1, PageSize=10)
            var validQuery = QueryInstanceFactory.CreateInstance(queryType, 1, 10, out var createErrors);
            if (createErrors.Count > 0)
            {
                failures.Add($"La query paginada '{queryType.Name}': error al crear instancia base: {string.Join("; ", createErrors)}");
                continue;
            }

            // 2. PageNumber = 0 debe fallar en la propiedad PageNumber
            var queryPageNum0 = QueryInstanceFactory.CreateInstance(queryType, 0, 10, out _);
            var resPageNum0 = validator.Validate(new ValidationContext<object>(queryPageNum0));
            if (!resPageNum0.Errors.Any(e => e.PropertyName == "PageNumber"))
            {
                failures.Add($"La query paginada '{queryType.Name}' no aplica la regla de 'PageNumber' (PageNumber=0 no produjo error de validación en la propiedad 'PageNumber').");
            }

            // 3. PageSize = 0 debe fallar en la propiedad PageSize
            var queryPageSize0 = QueryInstanceFactory.CreateInstance(queryType, 1, 0, out _);
            var resPageSize0 = validator.Validate(new ValidationContext<object>(queryPageSize0));
            if (!resPageSize0.Errors.Any(e => e.PropertyName == "PageSize"))
            {
                failures.Add($"La query paginada '{queryType.Name}' no aplica la regla de 'PageSize' (PageSize=0 no produjo error de validación en la propiedad 'PageSize').");
            }

            // 4. PageSize = MaxPageSize + 1 debe fallar en la propiedad PageSize
            var queryPageSizeOverMax = QueryInstanceFactory.CreateInstance(queryType, 1, PaginationRules.MaxPageSize + 1, out _);
            var resPageSizeOverMax = validator.Validate(new ValidationContext<object>(queryPageSizeOverMax));
            if (!resPageSizeOverMax.Errors.Any(e => e.PropertyName == "PageSize"))
            {
                failures.Add($"La query paginada '{queryType.Name}' no aplica la regla de 'PageSize' (PageSize={PaginationRules.MaxPageSize + 1} no produjo error de validación en la propiedad 'PageSize').");
            }

            // 5. PageSize = MaxPageSize debe ser válido para PageSize
            var queryPageSizeMax = QueryInstanceFactory.CreateInstance(queryType, 1, PaginationRules.MaxPageSize, out _);
            var resPageSizeMax = validator.Validate(new ValidationContext<object>(queryPageSizeMax));
            if (resPageSizeMax.Errors.Any(e => e.PropertyName == "PageSize"))
            {
                failures.Add($"La query paginada '{queryType.Name}' rechaza el valor máximo permitido ({PaginationRules.MaxPageSize}) en la propiedad 'PageSize'.");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n\n", failures));
    }

    [Fact]
    public void PaginationRules_IsTheOnlyAuthority()
    {
        SourceTreeIndex.EnsureNoSyntaxErrors();
        var files = SourceTreeIndex.ApplicationAndApiFiles;
        var violations = new List<string>();

        var allowedFolder = Path.Combine("src", "Pos.Application", "Common", "Validation");

        foreach (var file in files)
        {
            var relFileDir = Path.GetRelativePath(SolutionDirectory.Root, Path.GetDirectoryName(file.FilePath) ?? string.Empty);
            if (string.Equals(relFileDir, allowedFolder, StringComparison.OrdinalIgnoreCase))
                continue;

            var mathViolations = PaginationSyntaxScanner.FindMathPaginationInvocations(file.Root, file.FilePath);
            foreach (var v in mathViolations)
            {
                var relPath = Path.GetRelativePath(SolutionDirectory.Root, v.FilePath);
                violations.Add($"{relPath} (Línea {v.LineNumber}): {v.StatementText}");
            }

            if (PaginationSyntaxScanner.HasMaxPageSizeIdentifierToken(file.Root))
            {
                var relPath = Path.GetRelativePath(SolutionDirectory.Root, file.FilePath);
                violations.Add($"{relPath}: Se detectó el uso directo del identificador 'MaxPageSize' fuera de la carpeta '{allowedFolder}'.");
            }
        }

        Assert.True(violations.Count == 0,
            "Se detectó lógica de acotamiento/clamp de paginación manual directa fuera de PaginationRules:\n- " +
            string.Join("\n- ", violations));
    }
}
