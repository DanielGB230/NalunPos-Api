using System.Net;
using System.Text.Json;
using FluentAssertions;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

[Collection("IntegrationTests")]
public class PaginationHttpIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    private static readonly string[] RequiredTestPermissions =
    [
        Pos.Application.Common.Authorization.Permissions.Tenants.Create,
        Pos.Application.Common.Authorization.Permissions.Tenants.View,
        Pos.Application.Common.Authorization.Permissions.Users.Create,
        Pos.Application.Common.Authorization.Permissions.Users.View,
        Pos.Application.Common.Authorization.Permissions.Notifications.View
    ];

    public PaginationHttpIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── 2a. Query sin Guid (api/v1/users) ────────────────────────────────────

    [Fact]
    public async Task QueryWithoutGuid_PageSize_GreaterThan100_Returns400_WithCamelCaseErrorsKeyAndCorrelationId()
    {
        var (client, _, _) = await AuthenticatedClientFactory.CreateAuthenticatedClientAsync(_factory, permissions: RequiredTestPermissions);

        var response = await client.GetAsync("/api/v1/users?pageSize=101");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("status", out var statusEl).Should().BeTrue();
        statusEl.GetInt32().Should().Be(400);

        root.TryGetProperty("correlationId", out var correlationEl).Should().BeTrue();
        correlationEl.GetString().Should().NotBeNullOrEmpty();

        root.TryGetProperty("errors", out var errorsEl).Should().BeTrue("el ProblemDetails debe incluir la clave 'errors'");
        errorsEl.TryGetProperty("pageSize", out var pageSizeErrors).Should().BeTrue("PageSize debe reportarse en camelCase ('pageSize')");
        pageSizeErrors.ValueKind.Should().Be(JsonValueKind.Array);
        pageSizeErrors.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task QueryWithoutGuid_PageSize_Equals0_Returns400()
    {
        var (client, _, _) = await AuthenticatedClientFactory.CreateAuthenticatedClientAsync(_factory, permissions: RequiredTestPermissions);

        var response = await client.GetAsync("/api/v1/users?pageSize=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("errors", out var errorsEl).Should().BeTrue();
        errorsEl.TryGetProperty("pageSize", out _).Should().BeTrue("debe reportar error en 'pageSize'");
    }

    [Fact]
    public async Task QueryWithoutGuid_PageNumber_Equals0_Returns400()
    {
        var (client, _, _) = await AuthenticatedClientFactory.CreateAuthenticatedClientAsync(_factory, permissions: RequiredTestPermissions);

        var response = await client.GetAsync("/api/v1/users?pageNumber=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("errors", out var errorsEl).Should().BeTrue();
        errorsEl.TryGetProperty("pageNumber", out _).Should().BeTrue("debe reportar error en 'pageNumber'");
    }

    [Fact]
    public async Task QueryWithoutGuid_PageSize_Equals100_Returns200()
    {
        var (client, _, _) = await AuthenticatedClientFactory.CreateAuthenticatedClientAsync(_factory, permissions: RequiredTestPermissions);

        var response = await client.GetAsync("/api/v1/users?pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── 2b. Query con Guid (api/v1/notifications/user/{userId}) ─────────────

    [Fact]
    public async Task QueryWithGuid_PageSize_GreaterThan100_Returns400_WithCamelCaseErrorsKey()
    {
        var (client, user, _) = await AuthenticatedClientFactory.CreateAuthenticatedClientAsync(_factory, permissions: RequiredTestPermissions);

        var response = await client.GetAsync($"/api/v1/notifications/user/{user.Id}?pageSize=101");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("errors", out var errorsEl).Should().BeTrue("el ProblemDetails debe incluir la clave 'errors'");
        errorsEl.TryGetProperty("pageSize", out var pageSizeErrors).Should().BeTrue("PageSize debe reportarse en camelCase ('pageSize')");
        pageSizeErrors.ValueKind.Should().Be(JsonValueKind.Array);
        pageSizeErrors.GetArrayLength().Should().BeGreaterThan(0);
    }
}
