using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

[CollectionDefinition("IntegrationTests", DisableParallelization = true)]
public class IntegrationTestFixtureGroup : ICollectionFixture<MsSqlTestFixture>
{
}
