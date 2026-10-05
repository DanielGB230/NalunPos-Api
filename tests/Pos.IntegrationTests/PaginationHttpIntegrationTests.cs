using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence.Context;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

[Collection("IntegrationTests")]
public class PaginationHttpIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PaginationHttpIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private (HttpClient Client, Guid UserId) CreateAuthenticatedClient(Guid? tenantId = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.0.{Random.Shared.Next(1, 250)}.{Random.Shared.Next(1, 250)}");

        var config = _factory.Services.GetRequiredService<IConfiguration>();
        var generator = new JwtTokenGenerator(config);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        var roleId = tenantId.HasValue ? Guid.NewGuid() : Role.SuperAdminRoleId;
        var role = db.Roles.IgnoreQueryFilters().FirstOrDefault(r => r.Id == roleId);

        var permissions = new[]
        {
            Pos.Application.Common.Authorization.Permissions.Tenants.Create,
            Pos.Application.Common.Authorization.Permissions.Tenants.View,
            Pos.Application.Common.Authorization.Permissions.Users.Create,
            Pos.Application.Common.Authorization.Permissions.Users.View,
            Pos.Application.Common.Authorization.Permissions.Notifications.View
        };

        if (role == null)
        {
            var constructor = typeof(Role).GetConstructor(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(Guid), typeof(Guid), typeof(string), typeof(string), typeof(IEnumerable<string>) },
                null);

            role = (Role)constructor!.Invoke(new object[] { roleId, tenantId ?? Guid.Empty, "TestRole_" + Guid.NewGuid().ToString()[..6], "Test Role", permissions });
            db.Roles.Add(role);
        }
        else
        {
            foreach (var p in permissions)
            {
                role.AddPermission(p);
            }
        }

        var user = User.Create(
            new Email($"pagtest_{Guid.NewGuid().ToString()[..8]}@test.com"),
            new PasswordHash("hashedpassword"),
            roleId,
            tenantId,
            "PagTest",
            "User");

        db.Users.Add(user);
        db.SaveChangesAsync().GetAwaiter().GetResult();

        var token = generator.GenerateToken(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return (client, user.Id);
    }

    // ── 2a. Query sin Guid (api/v1/users) ────────────────────────────────────

    [Fact]
    public async Task QueryWithoutGuid_PageSize_GreaterThan100_Returns400_WithCamelCaseErrorsKeyAndCorrelationId()
    {
        var (client, _) = CreateAuthenticatedClient();

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
        var (client, _) = CreateAuthenticatedClient();

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
        var (client, _) = CreateAuthenticatedClient();

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
        var (client, _) = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/users?pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── 2b. Query con Guid (api/v1/notifications/user/{userId}) ─────────────

    [Fact]
    public async Task QueryWithGuid_PageSize_GreaterThan100_Returns400_WithCamelCaseErrorsKey()
    {
        var (client, userId) = CreateAuthenticatedClient();

        var response = await client.GetAsync($"/api/v1/notifications/user/{userId}?pageSize=101");

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
