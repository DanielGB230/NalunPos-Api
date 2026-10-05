using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Pos.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Pos.IntegrationTests;

[Collection("IntegrationTests")]
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

    [Fact]
    public async Task PlatformEndpoints_WhenCalledByTenantUser_ShouldReturn403Forbidden()
    {
        // Arrange
        var (client, _, _) = await AuthenticatedClientFactory.CreateAuthenticatedClientAsync(_factory, tenantId: Guid.NewGuid());

        // Act
        var response = await client.GetAsync("/api/v1/platform/tenants");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUserNotifications_WhenCalledForAnotherUser_ShouldReturn403Forbidden()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        var (client, _, _) = await AuthenticatedClientFactory.CreateAuthenticatedClientAsync(_factory, tenantId: tenantId);
        Guid userBId = Guid.NewGuid();

        // Act - User A tries to read User B's notifications
        var response = await client.GetAsync($"/api/v1/Notifications/user/{userBId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUserNotifications_WhenCalledForSelf_ShouldNotReturnForbiddenOrUnauthorized()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        var (client, userA, _) = await AuthenticatedClientFactory.CreateAuthenticatedClientAsync(_factory, tenantId: tenantId);

        // Act - User A reads their own notifications
        var response = await client.GetAsync($"/api/v1/Notifications/user/{userA.Id}");

        // Assert
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
