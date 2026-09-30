using System.Net;
using FluentAssertions;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

[Collection("IntegrationTests")]
public class CorrelationIdIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CorrelationIdIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RequestWithoutCorrelationId_GeneratesNewOneAndReturnsInHeader()
    {
        var client = _factory.CreateClient();
        
        var response = await client.PostAsync("/api/v1/auth/login", new StringContent(""));
        
        response.Headers.Contains("X-Correlation-ID").Should().BeTrue();
        response.Headers.GetValues("X-Correlation-ID").First().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RequestWithCorrelationId_ReturnsSameCorrelationIdInHeader()
    {
        var client = _factory.CreateClient();
        string inputCorrelationId = "test-correlation-id-12345";
        
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = new StringContent("") };
        request.Headers.Add("X-Correlation-ID", inputCorrelationId);

        var response = await client.SendAsync(request);
        
        response.Headers.Contains("X-Correlation-ID").Should().BeTrue();
        response.Headers.GetValues("X-Correlation-ID").First().Should().Be(inputCorrelationId);
    }
}
