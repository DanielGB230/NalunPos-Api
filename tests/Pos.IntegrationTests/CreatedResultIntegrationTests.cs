using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Pos.Application.Common.Authorization;
using Pos.IntegrationTests.Fixtures;

namespace Pos.IntegrationTests;

[Collection("IntegrationTests")]
public class CreatedResultIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    private static readonly string[] RequiredTestPermissions =
    [
        Permissions.Categories.Create,
        Permissions.Branches.Create
    ];

    public CreatedResultIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateCategory_Returns201Created_WithLocationHeader_AndEntityBody()
    {
        // Arrange
        var (client, _, _) = await AuthenticatedClientFactory.CreateAuthenticatedClientAsync(_factory, permissions: RequiredTestPermissions);
        var requestBody = new
        {
            name = $"Category_Created_{Guid.NewGuid():N}"[..20],
            description = "Test description for 201 Created verification"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/categories", requestBody);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull("un endpoint de alta exitoso debe devolver la cabecera HTTP Location");

        var locationUri = response.Headers.Location!.ToString();
        locationUri.Should().Contain("/api/v1/categories/");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("id", out var idProp).Should().BeTrue("el cuerpo de respuesta de 201 Created debe incluir el Id generado");
        var categoryId = idProp.GetString();
        categoryId.Should().NotBeNullOrWhiteSpace();

        locationUri.Should().EndWith(categoryId);
    }

    [Fact]
    public async Task CreateCategory_WhenDuplicateName_Returns409Conflict_WithoutThrowing_AndLocationHeaderIsNull()
    {
        // Arrange
        var (client, _, _) = await AuthenticatedClientFactory.CreateAuthenticatedClientAsync(_factory, permissions: RequiredTestPermissions);
        var uniqueName = $"Duplicate_{Guid.NewGuid():N}"[..18];
        var requestBody = new
        {
            name = uniqueName,
            description = "Initial category creation"
        };

        var firstResponse = await client.PostAsJsonAsync("/api/v1/categories", requestBody);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act - Attempt duplicate category creation to trigger Conflict (409)
        var duplicateResponse = await client.PostAsJsonAsync("/api/v1/categories", requestBody);

        // Assert
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        duplicateResponse.Headers.Location.Should().BeNull("un endpoint con fallo no debe incluir cabecera Location");

        var json = await duplicateResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("status", out var statusProp).Should().BeTrue();
        statusProp.GetInt32().Should().Be(409);

        root.TryGetProperty("title", out var titleProp).Should().BeTrue();
        titleProp.GetString().Should().NotBeNullOrWhiteSpace();
    }
}
