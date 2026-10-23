namespace Pos.IntegrationTests;

using Pos.IntegrationTests.Fixtures;
using Xunit;

public class MsSqlTestFixtureSecurityTests
{
    [Theory]
    [InlineData("Server=localhost;Database=NalunPosDev;User Id=sa;Password=pass;TrustServerCertificate=True;")]
    [InlineData("Server=localhost;Database=NalunPosProduction;User Id=sa;Password=pass;TrustServerCertificate=True;")]
    [InlineData("Server=localhost;Database=NalunPos;User Id=sa;Password=pass;TrustServerCertificate=True;")]
    [InlineData("Server=localhost;Database=NalunPosTestsDb;User Id=sa;Password=pass;TrustServerCertificate=True;")]
    public void ValidateDatabaseName_ThrowsInvalidOperationException_WhenDatabaseNameDoesNotEndWithTests(string connectionString)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MsSqlTestFixture.ValidateDatabaseName(connectionString));

        Assert.Contains("debe terminar en 'Tests'", ex.Message);
    }

    [Theory]
    [InlineData("Server=localhost;Database=PosIntegrationTests;User Id=sa;Password=pass;TrustServerCertificate=True;")]
    [InlineData("Server=localhost;Database=PosInfrastructureTests;User Id=sa;Password=pass;TrustServerCertificate=True;")]
    [InlineData("Server=localhost;Database=NalunPos_IntegrationTests;User Id=sa;Password=pass;TrustServerCertificate=True;")]
    public void ValidateDatabaseName_Succeeds_WhenDatabaseNameEndsWithTests(string connectionString)
    {
        // No debe lanzar excepción
        MsSqlTestFixture.ValidateDatabaseName(connectionString);
    }
}
