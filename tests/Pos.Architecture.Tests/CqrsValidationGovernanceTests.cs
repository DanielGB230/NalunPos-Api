using System;
using System.Collections.Generic;
using System.Linq;
using Pos.Architecture.Tests.Support;
using Xunit;

namespace Pos.Architecture.Tests;

public class CqrsValidationGovernanceTests
{
    [Fact]
    public void EveryCommand_HasItsOwnValidatorInSeparateFile()
    {
        var commandTypes = CqrsTypeCatalog.GetCommands();
        var failures = new List<string>();

        foreach (var commandType in commandTypes)
        {
            var commandFailures = ValidatorPlacementRule.ValidatePlacement(commandType, "Command");
            failures.AddRange(commandFailures);
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void EveryNonExemptQuery_HasItsOwnValidatorInSeparateFile()
    {
        var queryTypes = CqrsTypeCatalog.GetQueries();
        var failures = new List<string>();

        foreach (var queryType in queryTypes)
        {
            if (CqrsTypeCatalog.IsExemptQuery(queryType))
            {
                continue;
            }

            var queryFailures = ValidatorPlacementRule.ValidatePlacement(queryType, "Query");
            failures.AddRange(queryFailures);
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
