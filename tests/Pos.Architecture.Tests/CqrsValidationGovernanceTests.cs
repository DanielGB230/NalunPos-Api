using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Pos.Architecture.Tests.Support;
using Xunit;

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
    public void EveryQuery_HasItsOwnValidatorInSeparateFile_WithZeroExceptions()
    {
        SourceTreeIndex.EnsureNoSyntaxErrors();
        var queries = CqrsTypeCatalog.GetQueries();

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

    [Fact]
    public void Every_Cqrs_Message_Command_And_Query_Has_A_Concrete_AbstractValidator()
    {
        var messages = CqrsTypeCatalog.GetCommands().Concat(CqrsTypeCatalog.GetQueries()).ToList();
        var missingValidators = new List<string>();

        foreach (var msg in messages)
        {
            var validatorType = CqrsTypeCatalog.GetValidatorFor(msg);
            if (validatorType == null)
            {
                missingValidators.Add($"El mensaje CQRS '{msg.FullName}' no tiene validador registrado en Pos.Application.");
                continue;
            }

            var genericAbstractValidator = typeof(AbstractValidator<>).MakeGenericType(msg);
            if (!genericAbstractValidator.IsAssignableFrom(validatorType))
            {
                missingValidators.Add($"El validador '{validatorType.FullName}' para '{msg.FullName}' no hereda de AbstractValidator<{msg.Name}>.");
            }
        }

        Assert.Empty(missingValidators);
    }
}
