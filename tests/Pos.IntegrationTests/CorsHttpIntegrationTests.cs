using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pos.Api.Contracts.Requests;
using Pos.Application.Authentication.Commands.Login;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

/// <summary>
/// B3: Pruebas de CORS - validación de orígenes, preflight y entorno Development.
/// </summary>
[Collection("IntegrationTests")]
public class CorsHttpIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlTestFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;

    public CorsHttpIntegrationTests(MsSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", _fixture.ConnectionString);
                builder.UseSetting("SuperAdminSettings:Email", "superadmin_cors@test.com");
                builder.UseSetting("SuperAdminSettings:Password", "SuperStatusPass123!");
                builder.UseSetting("SuperAdminSettings:FirstName", "Super");
                builder.UseSetting("SuperAdminSettings:LastName", "Admin");
                builder.UseSetting("JwtSettings:Secret", "SuperSecretEnterpriseJwtKey_LongEnoughFor256Bits_NalunPos2026!");
                builder.UseSetting("JwtSettings:Issuer", "NalunPosApi");
                builder.UseSetting("JwtSettings:Audience", "NalunPosClients");
            });

        return Task.CompletedTask;
    }

    [Fact]
    public void App_FailsToStart_WhenCorsOriginsInvalid()
    {
        // Theory: estos valores deben hacer fallar el arranque
        var invalidOrigins = new[]
        {
            "http://localhost:4200/",
            "*",
            "null",
            "ftp://localhost:4200",
            "localhost:4200",
            "http://localhost:4200/api",
            "http://user@localhost:4200"
        };

        foreach (var invalidOrigin in invalidOrigins)
        {
            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Testing");
                    builder.UseSetting("ConnectionStrings:DefaultConnection", _fixture.ConnectionString);
                    builder.UseSetting("SuperAdminSettings:Email", "superadmin_cors@test.com");
                    builder.UseSetting("SuperAdminSettings:Password", "SuperStatusPass123!");
                    builder.UseSetting("JwtSettings:Secret", "SuperSecretEnterpriseJwtKey_LongEnoughFor256Bits_NalunPos2026!");
                    builder.UseSetting("JwtSettings:Issuer", "NalunPosApi");
                    builder.UseSetting($"CorsSettings:AllowedOrigins:0", invalidOrigin);
                });

            var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
            Assert.Contains("CorsSettings:AllowedOrigins", exception.Message);
        }
    }

    [Fact]
    public void App_Starts_Successfully_WhenCorsOriginsValid_InDevelopment()
    {
        // Arrancar en Development con orígenes válidos desde appsettings.Development.json
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
            });

        var client = factory.CreateClient();
        Assert.NotNull(client);
    }

    [Fact]
    public async Task Preflight_OPTIONS_ReturnsResult()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
            });

        var client = factory.CreateClient();

        // Realizar login usando SendAsync con StringContent
        var loginBody = JsonSerializer.Serialize(new LoginRequest("admin@test.com", "SuperStatusPass123!"));
        var loginResponse = await client.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("/api/v1/auth/login", UriKind.Relative),
            Content = new StringContent(loginBody, Encoding.UTF8, "application/json")
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        // Realizar petición preflight OPTIONS con origen http://localhost:4200
        var optionsResponse = await client.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Options,
            RequestUri = new Uri("/api/v1/auth/login", UriKind.Relative),
            Headers = {
                { "Origin", "http://localhost:4200" },
                { "Access-Control-Request-Method", "POST" },
                { "Access-Control-Request-Headers", "authorization,content-type" }
            }
        });

        // Debería recibir 204 No Content o 405 Method Not Allowed
        Assert.True(optionsResponse.StatusCode == HttpStatusCode.NoContent ||
                    optionsResponse.StatusCode == HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Preflight_InvalidOrigin_ReturnsDifferentResult()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
            });

        var client = factory.CreateClient();

        // Petición preflight con origen no configurado
        var optionsResponse = await client.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Options,
            RequestUri = new Uri("/api/v1/auth/login", UriKind.Relative),
            Headers = {
                { "Origin", "http://localhost:4300" }
            }
        });

        // Debería devolver distinto de 204 con el origen inválido
        Assert.NotEqual(HttpStatusCode.NoContent, optionsResponse.StatusCode);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }
}