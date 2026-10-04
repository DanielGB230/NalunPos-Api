using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pos.Api.Contracts.Requests;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Users.DTOs;
using Pos.Application.Users.Queries;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

// ── Controller de Prueba HTTP para Paginación ───────────────────────────────

[AllowAnonymous]
[ApiController]
[Route("_test_pagination")]
public sealed class PaginationHttpTestController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public PaginationHttpTestController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] GetUsersRequest request,
        CancellationToken cancellationToken)
    {
        var query = new GetUsersQuery(
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            request.IsActive);

        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return Ok(result);
    }
}

// ── Tests de Integración HTTP de Paginación ──────────────────────────────────

public class PaginationHttpIntegrationTests
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly User TestUser = User.Create("pagtest@example.com", "hash", Guid.NewGuid(), Guid.NewGuid(), "Admin", "User");

    public PaginationHttpIntegrationTests()
    {
        _factory = new CustomWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Registrar el controller de prueba en ASP.NET Core MVC ApplicationParts
                    var partManagerDescriptor = services.FirstOrDefault(
                        sd => sd.ServiceType == typeof(ApplicationPartManager));

                    if (partManagerDescriptor?.ImplementationInstance is ApplicationPartManager partManager)
                    {
                        if (!partManager.ApplicationParts.OfType<AssemblyPart>()
                                .Any(p => p.Assembly == typeof(PaginationHttpTestController).Assembly))
                        {
                            partManager.ApplicationParts.Add(new AssemblyPart(typeof(PaginationHttpTestController).Assembly));
                        }
                    }

                    // Registrar fakes de autorización para que GetUsersQuery pase AuthorizationQueryBehavior
                    services.AddScoped<ICurrentUserService>(_ => new FakeTestCurrentUserService(TestUserId));
                    services.AddScoped<IUserRepository>(_ => new FakeTestUserRepository(TestUser));
                    services.AddScoped<ICurrentUserPermissions>(_ => new FakeTestCurrentUserPermissions());
                });
            });
    }

    [Fact]
    public async Task PageSize_GreaterThan100_Returns400_WithCamelCaseErrorsKeyAndCorrelationId()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/_test_pagination?pageSize=101");

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
    public async Task PageSize_Equals0_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/_test_pagination?pageSize=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("errors", out var errorsEl).Should().BeTrue();
        errorsEl.TryGetProperty("pageSize", out _).Should().BeTrue("debe reportar error en 'pageSize'");
    }

    [Fact]
    public async Task PageNumber_Equals0_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/_test_pagination?pageNumber=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("errors", out var errorsEl).Should().BeTrue();
        errorsEl.TryGetProperty("pageNumber", out _).Should().BeTrue("debe reportar error en 'pageNumber'");
    }

    [Fact]
    public async Task PageSize_Equals100_Returns200()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/_test_pagination?pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Fakes Internos para el Test ─────────────────────────────────────────

    private sealed class FakeTestCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; }
        public string? UserEmail => "pagtest@example.com";

        public FakeTestCurrentUserService(Guid userId)
        {
            UserId = userId;
        }
    }

    private sealed class FakeTestUserRepository : IUserRepository
    {
        private readonly User _user;

        public FakeTestUserRepository(User user)
        {
            _user = user;
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<User?>(_user);

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult<User?>(_user);

        public Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Update(User user) { }

        public Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(
            int pageNumber,
            int pageSize,
            string? searchTerm,
            bool? isActive = null,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<User> emptyList = Array.Empty<User>();
            return Task.FromResult((Items: emptyList, TotalCount: 0));
        }
    }

    private sealed class FakeTestCurrentUserPermissions : ICurrentUserPermissions
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid? tenantId, Guid roleId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { Permissions.Users.View });

        public Task<bool> HasPermissionAsync(Guid? tenantId, Guid roleId, string permission, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
