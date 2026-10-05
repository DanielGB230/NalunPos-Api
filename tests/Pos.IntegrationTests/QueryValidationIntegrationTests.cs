using System.Net;
using System.Text.Json;
using FluentAssertions;
using Pos.Application.Common.Authorization;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

[Collection("IntegrationTests")]
public class QueryValidationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    private static readonly string[] RequiredTestPermissions =
    [
        Permissions.Users.View,
        Permissions.Payments.View
    ];

    public QueryValidationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetUserById_WithEmptyGuid_Returns400_WithErrorsIdAndCorrelationId()
    {
        var (client, _, _) = await AuthenticatedClientFactory.CreateAuthenticatedClientAsync(_factory, permissions: RequiredTestPermissions);

        var response = await client.GetAsync($"/api/v1/users/{Guid.Empty}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("status", out var statusEl).Should().BeTrue();
        statusEl.GetInt32().Should().Be(400);

        root.TryGetProperty("correlationId", out var correlationEl).Should().BeTrue();
        correlationEl.GetString().Should().NotBeNullOrEmpty();

        root.TryGetProperty("errors", out var errorsEl).Should().BeTrue("el ProblemDetails debe incluir la clave 'errors'");
        errorsEl.TryGetProperty("id", out var idErrors).Should().BeTrue("Id debe reportarse en camelCase ('id')");
        idErrors.ValueKind.Should().Be(JsonValueKind.Array);
        idErrors.GetArrayLength().Should().BeGreaterThan(0);
        idErrors[0].GetString().Should().Be("El identificador es obligatorio y no puede estar vacío.");
    }

    [Fact]
    public async Task GetPaymentsBySaleId_WithEmptyGuid_Returns400_WithErrorsSaleIdAndCorrelationId()
    {
        var (client, _, _) = await AuthenticatedClientFactory.CreateAuthenticatedClientAsync(_factory, permissions: RequiredTestPermissions);

        var response = await client.GetAsync($"/api/v1/payments/sale/{Guid.Empty}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("status", out var statusEl).Should().BeTrue();
        statusEl.GetInt32().Should().Be(400);

        root.TryGetProperty("correlationId", out var correlationEl).Should().BeTrue();
        correlationEl.GetString().Should().NotBeNullOrEmpty();

        root.TryGetProperty("errors", out var errorsEl).Should().BeTrue("el ProblemDetails debe incluir la clave 'errors'");
        errorsEl.TryGetProperty("saleId", out var saleIdErrors).Should().BeTrue("SaleId debe reportarse en camelCase ('saleId')");
        saleIdErrors.ValueKind.Should().Be(JsonValueKind.Array);
        saleIdErrors.GetArrayLength().Should().BeGreaterThan(0);
        saleIdErrors[0].GetString().Should().Be("El identificador es obligatorio y no puede estar vacío.");
    }
}
