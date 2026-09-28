using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

[Collection("Integration")]
public class RateLimitingIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RateLimitingIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousEndpoint_WhenLimitExceeded_ShouldReturn429WithProblemDetailsAndRetryAfter()
    {
        var client = _factory.CreateClient();
        
        var requestContent = new StringContent("{\"email\":\"test@test.com\",\"password\":\"TestPassword123!\"}", System.Text.Encoding.UTF8, "application/json");

        // Agotar cuota de AuthPolicy (límite 5 en Testing json)
        for (int i = 0; i < 5; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = requestContent };
            var response = await client.SendAsync(req);
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        // 6ta request debe fallar
        var failingReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = requestContent };
        var failingResponse = await client.SendAsync(failingReq);

        failingResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        failingResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        
        failingResponse.Headers.Contains("Retry-After").Should().BeTrue();
        int retryAfter = int.Parse(failingResponse.Headers.GetValues("Retry-After").First(), System.Globalization.CultureInfo.InvariantCulture);
        retryAfter.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TenantPartitioning_ExhaustingQuotaForTenantA_DoesNotAffectTenantB()
    {
        var clientA = _factory.CreateClient();
        var clientB = _factory.CreateClient();
        
        // Simular distinta IP
        clientA.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.50");
        clientB.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.51");

        var requestContent = new StringContent("{\"email\":\"test@test.com\",\"password\":\"Test\"}", System.Text.Encoding.UTF8, "application/json");

        for (int i = 0; i < 5; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = requestContent };
            await clientA.SendAsync(req);
        }

        var failReqA = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = requestContent };
        var resA = await clientA.SendAsync(failReqA);
        resA.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var reqB = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = requestContent };
        var resB = await clientB.SendAsync(reqB);
        resB.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }
}
