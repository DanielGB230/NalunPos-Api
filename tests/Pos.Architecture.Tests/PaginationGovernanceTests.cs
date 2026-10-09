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
                failures.Add($"La query paginada '{queryType.Name}' no tiene un validador registrado en la aplicación.");
                continue;
            }

            var ctor = CqrsTypeCatalog.GetPrimaryConstructor(validatorType);
            var ctorBody = ctor?.GetMethodBody();
            var ilBytes = ctorBody?.GetILAsByteArray() ?? Array.Empty<byte>();

            var appliesRules = ilBytes.Length > 0;
            if (!appliesRules)
            {
                failures.Add($"La query paginada '{queryType.Name}' no invoca 'PaginationRules.ApplyPageSizeRule' en el constructor de su validador '{validatorType.Name}'.");
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

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file.FilePath);
            if (fileName == "PaginationRules.cs")
                continue;

            var mathViolations = PaginationSyntaxScanner.FindMathPaginationInvocations(file.Root, file.FilePath);
            foreach (var v in mathViolations)
            {
                var relPath = Path.GetRelativePath(SolutionDirectory.Root, v.FilePath);
                violations.Add($"{relPath} (Línea {v.LineNumber}): {v.StatementText}");
            }
        }

        Assert.True(violations.Count == 0,
            "Se detectó lógica de acotamiento/clamp de paginación manual directa fuera de PaginationRules:\n- " +
            string.Join("\n- ", violations));
    }
}
