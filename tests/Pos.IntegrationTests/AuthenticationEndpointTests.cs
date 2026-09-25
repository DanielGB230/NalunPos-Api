using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

using Microsoft.Extensions.Configuration;
using Pos.IntegrationTests.Fixtures;
using Pos.Domain.ValueObjects;

namespace Pos.IntegrationTests;

public class AuthenticationEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public AuthenticationEndpointTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    private static readonly HashSet<string> AllowedAnonymousEndpoints = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/v1/auth/login",
        "/api/v1/auth/refresh",
        "/api/v1/auth/register"
    };

    [Fact]
    public async Task AllEndpoints_WhenCalledWithoutToken_ShouldReturn401Unauthorized_UnlessAllowAnonymous()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var endpointDataSource = _factory.Services.GetRequiredService<EndpointDataSource>();
        var routeEndpoints = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() != null)
            .ToList();

        var results = new List<(string Method, string RoutePattern, int StatusCode, bool HasAllowAnonymous, bool IsAuthorized)>();

        // Act
        foreach (var endpoint in routeEndpoints)
        {
            var descriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
            if (descriptor == null) continue;

            var httpMethodMetadata = endpoint.Metadata.GetMetadata<HttpMethodMetadata>();
            string httpMethod = (httpMethodMetadata != null && httpMethodMetadata.HttpMethods.Count > 0) ? httpMethodMetadata.HttpMethods[0] : "GET";

            string routePattern = "/" + endpoint.RoutePattern.RawText?.TrimStart('/');

            // Replace route parameters with dummy values for route matching
            string sampleUrl = routePattern;
            sampleUrl = System.Text.RegularExpressions.Regex.Replace(sampleUrl, @"\{id(?::[^}]+)?\}", Guid.NewGuid().ToString(), System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            sampleUrl = System.Text.RegularExpressions.Regex.Replace(sampleUrl, @"\{tenantId(?::[^}]+)?\}", Guid.NewGuid().ToString(), System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            sampleUrl = System.Text.RegularExpressions.Regex.Replace(sampleUrl, @"\{[^}]+}", "1");

            var request = new HttpRequestMessage(new HttpMethod(httpMethod), sampleUrl);

            // Execute HTTP call without token
            var response = await client.SendAsync(request);
            int statusCode = (int)response.StatusCode;

            bool hasAllowAnonymous = descriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any() ||
                                     descriptor.ControllerTypeInfo.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Length > 0;

            bool isAllowed = AllowedAnonymousEndpoints.Contains(routePattern) || hasAllowAnonymous;

            results.Add((httpMethod, routePattern, statusCode, isAllowed, statusCode == (int)HttpStatusCode.Unauthorized || (isAllowed && statusCode != (int)HttpStatusCode.Unauthorized)));
        }

        // Print Real Output Table for Audit
        _output.WriteLine("| HTTP Method | Endpoint Route Pattern | Status Code | AllowAnonymous | Auth Check Pass |");
        _output.WriteLine("|---|---|---|---|---|");
        foreach (var r in results.OrderBy(x => x.RoutePattern))
        {
            _output.WriteLine($"| {r.Method} | `{r.RoutePattern}` | `{r.StatusCode}` | {r.HasAllowAnonymous} | {(r.StatusCode == 401 ? "✅ 401" : r.HasAllowAnonymous ? "⚠️ AllowAnon" : "❌ UNPROTECTED (" + r.StatusCode + ")")} |");
        }

        // Assert: All non-anonymous endpoints MUST return 401 Unauthorized
        var unprotectedEndpoints = results.Where(r => !r.HasAllowAnonymous && r.StatusCode != (int)HttpStatusCode.Unauthorized).ToList();

        Assert.Empty(unprotectedEndpoints);
    }

    private string GenerateJwtToken(Guid roleId, Guid? tenantId = null, Guid? overrideUserId = null)
    {
        var config = _factory.Services.GetRequiredService<IConfiguration>();
        var generator = new Pos.Infrastructure.Authentication.JwtTokenGenerator(config);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Pos.Infrastructure.Persistence.Context.PosDbContext>();

        var role = db.Roles.FirstOrDefault(r => r.Id == roleId);
        if (role == null)
        {
            role = Pos.Domain.Entities.Role.Create(tenantId ?? Guid.Empty, "TestRole_" + Guid.NewGuid().ToString()[..6], "Test Role");
            db.Roles.Add(role);
            roleId = role.Id;
        }

        var emailStr = $"test_{Guid.NewGuid().ToString()[..8]}@domain.com";
        var user = Pos.Domain.Entities.User.Create(
            new Email(emailStr),
            new PasswordHash("hashedpassword"),
            roleId,
            tenantId,
            "Test",
            "User"
        );

        if (overrideUserId.HasValue)
        {
            typeof(Pos.Domain.Common.Entity<Guid>).GetProperty(nameof(Pos.Domain.Entities.User.Id))!.SetValue(user, overrideUserId.Value);
        }

        db.Users.Add(user);
        db.SaveChangesAsync().GetAwaiter().GetResult();

        return generator.GenerateToken(user);
    }

    [Fact]
    public async Task PlatformEndpoints_WhenCalledByTenantUser_ShouldReturn403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        string token = GenerateJwtToken(Guid.NewGuid(), tenantId: Guid.NewGuid());
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/v1/platform/tenants");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }



    [Fact]
    public async Task GetUserNotifications_WhenCalledForAnotherUser_ShouldReturn403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        Guid userAId = Guid.NewGuid();
        Guid userBId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        string tokenUserA = GenerateJwtToken(Guid.NewGuid(), tenantId: tenantId, overrideUserId: userAId);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenUserA);

        // Act - User A tries to read User B's notifications
        var response = await client.GetAsync($"/api/Notifications/user/{userBId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUserNotifications_WhenCalledForSelf_ShouldNotReturnForbiddenOrUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        Guid userAId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        string tokenUserA = GenerateJwtToken(Guid.NewGuid(), tenantId: tenantId, overrideUserId: userAId);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenUserA);

        // Act - User A reads their own notifications
        var response = await client.GetAsync($"/api/Notifications/user/{userAId}");

        // Assert
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
