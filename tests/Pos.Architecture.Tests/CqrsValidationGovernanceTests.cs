using Pos.Architecture.Tests.Support;

namespace Pos.Architecture.Tests;

public class CqrsValidationGovernanceTests
{
    [Fact]
    public void EveryCommand_HasItsOwnValidatorInSeparateFile()
    {
        SourceTreeIndex.EnsureNoSyntaxErrors();
        var commands = CqrsTypeCatalog.GetCommands();
        var failures = new List<string>();

        foreach (var command in commands)
        {
            var placementFailures = ValidatorPlacementRule.ValidatePlacement(command);
            if (placementFailures.Count > 0)
            {
                failures.Add($"El comando '{command.Name}' debe tener su validador '{command.Name}Validator' en su propio archivo '{command.Name}Validator.cs' en la misma carpeta. Fallas:\n- {string.Join("\n- ", placementFailures)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n\n", failures));
    }

    [Fact]
    public void EveryNonExemptQuery_HasItsOwnValidatorInSeparateFile()
    {
        SourceTreeIndex.EnsureNoSyntaxErrors();
        var queries = CqrsTypeCatalog.GetQueries()
            .Where(q => !CqrsTypeCatalog.IsExemptQuery(q));

        var failures = new List<string>();

        foreach (var query in queries)
        {
            var placementFailures = ValidatorPlacementRule.ValidatePlacement(query);
            if (placementFailures.Count > 0)
            {
                failures.Add($"La query '{query.Name}' debe tener su validador '{query.Name}Validator' en su propio archivo '{query.Name}Validator.cs' en la misma carpeta. Fallas:\n- {string.Join("\n- ", placementFailures)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n\n", failures));
    }
}
