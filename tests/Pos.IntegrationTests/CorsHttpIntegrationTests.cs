using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

/// <summary>
/// B3: validación fail-fast de los orígenes CORS y comportamiento de la política
/// "AllowFrontend" (ADR 0035). La validación se ejecuta en el arranque: una lista de
/// orígenes vacía o con algún origen inválido aborta el inicio de la API.
/// </summary>
[Collection("IntegrationTests")]
public class CorsHttpIntegrationTests : IAsyncLifetime
{
    private const string AllowedOrigin = "http://localhost:4200";
    private const string SecondDevelopmentOrigin = "http://localhost:4201";
    private const string ForbiddenOrigin = "http://localhost:4300";

    private readonly MsSqlTestFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;

    public CorsHttpIntegrationTests(MsSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = CreateFactory("Testing");
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// Fábrica base con la configuración mínima obligatoria para arrancar la API
    /// (Jwt, SuperAdmin, conexión y orígenes CORS válidos). Con
    /// <paramref name="useConfiguredCorsOrigin"/> en <c>false</c> no se declara ningún
    /// origen y se usa la configuración del entorno (appsettings.Development.json).
    /// </summary>
    private WebApplicationFactory<Program> CreateFactory(
        string environment,
        Action<IWebHostBuilder>? configure = null,
        bool useConfiguredCorsOrigin = true)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);
                builder.UseSetting("ConnectionStrings:DefaultConnection", _fixture.ConnectionString);
                builder.UseSetting("SuperAdminSettings:Email", "superadmin_cors@test.com");
                builder.UseSetting("SuperAdminSettings:Password", "SuperStatusPass123!");
                builder.UseSetting("SuperAdminSettings:FirstName", "Super");
                builder.UseSetting("SuperAdminSettings:LastName", "Admin");
                builder.UseSetting("JwtSettings:Secret", "SuperSecretEnterpriseJwtKey_LongEnoughFor256Bits_NalunPos2026!");
                builder.UseSetting("JwtSettings:Issuer", "NalunPosApi");
                builder.UseSetting("JwtSettings:Audience", "NalunPosClients");

                if (useConfiguredCorsOrigin)
                {
                    builder.UseSetting("CorsSettings:AllowedOrigins:0", AllowedOrigin);
                }

                // Mismo patrón que CustomWebApplicationFactory: la colección en memoria se
                // añade al final y tiene prioridad sobre appsettings/user-secrets para las
                // claves de infraestructura. Así las pruebas de Development usan la base de
                // pruebas y no la base de desarrollo. Los orígenes CORS se dejan en
                // UseSetting para que el override de cada prueba siga teniendo prioridad.
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = _fixture.ConnectionString,
                        ["SuperAdminSettings:Email"] = "superadmin_cors@test.com",
                        ["SuperAdminSettings:Password"] = "SuperStatusPass123!",
                        ["SuperAdminSettings:FirstName"] = "Super",
                        ["SuperAdminSettings:LastName"] = "Admin",
                        ["JwtSettings:Secret"] = "SuperSecretEnterpriseJwtKey_LongEnoughFor256Bits_NalunPos2026!",
                        ["JwtSettings:Issuer"] = "NalunPosApi",
                        ["JwtSettings:Audience"] = "NalunPosClients"
                    });
                });

                configure?.Invoke(builder);
            });
    }

    [Theory]
    [InlineData("http://localhost:4200/")]
    [InlineData("*")]
    [InlineData("null")]
    [InlineData("ftp://localhost:4200")]
    [InlineData("localhost:4200")]
    [InlineData("http://localhost:4200/api")]
    [InlineData("http://user@localhost:4200")]
    public void App_FailsToStart_WhenCorsOriginIsInvalid(string invalidOrigin)
    {
        using var factory = CreateFactory("Testing", builder =>
            builder.UseSetting("CorsSettings:AllowedOrigins:0", invalidOrigin));

        Exception exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        // El mensaje identifica la clave de configuración y el origen ofensor.
        Assert.Contains("CorsSettings:AllowedOrigins", exception.ToString());
        Assert.Contains(invalidOrigin, exception.ToString());
    }

    [Fact]
    public void App_FailsToStart_WhenCorsOriginsAreMissing()
    {
        using var factory = CreateFactory("Testing", builder =>
        {
            builder.UseSetting("CorsSettings:AllowedOrigins:0", "");
            builder.UseSetting("CorsSettings:AllowedOrigins:1", "");
        });

        Exception exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("CorsSettings:AllowedOrigins", exception.ToString());
    }

    [Fact]
    public void App_Starts_WhenCorsOriginsAreValid()
    {
        using var factory = CreateFactory("Testing");
        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }

    [Fact]
    public void App_Starts_InDevelopment_WithDevelopmentOrigins()
    {
        // Sin override: los orígenes provienen de appsettings.Development.json (4200/4201).
        using var factory = CreateFactory("Development", useConfiguredCorsOrigin: false);
        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }

    [Fact]
    public async Task Preflight_FromDevelopmentOrigin4201_ReturnsNoContentAndSentOrigin()
    {
        // 4201 sólo está configurado en appsettings.Development.json.
        using var factory = CreateFactory("Development", useConfiguredCorsOrigin: false);
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", SecondDevelopmentOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(SecondDevelopmentOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Preflight_WithAllowedOrigin_ReturnsAllowCredentialsTrue()
    {
        using var factory = CreateFactory("Testing");
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [Theory]
    [InlineData("DELETE")]
    [InlineData("PUT")]
    public async Task Preflight_WithDisallowedRequestMethod_DoesNotAllowIt(string method)
    {
        // La política sólo admite GET, POST, PATCH y OPTIONS: DELETE y PUT no deben
        // aparecer en Access-Control-Allow-Methods aunque el origen esté permitido.
        using var factory = CreateFactory("Testing");
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", method);

        using var response = await client.SendAsync(request);

        string allowedMethods = response.Headers.TryGetValues("Access-Control-Allow-Methods", out var values)
            ? string.Join(",", values)
            : string.Empty;

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(method, allowedMethods, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AllowedOrigin)]
    [InlineData(SecondDevelopmentOrigin)]
    public async Task Preflight_InDevelopmentWithoutOverride_AllowsBothFrontendOrigins(string origin)
    {
        // Sin override de CORS: 4200 y 4201 provienen de appsettings.Development.json.
        using var factory = CreateFactory("Development", useConfiguredCorsOrigin: false);
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Preflight_WithNotAllowedOrigin_HasNoAllowOriginHeader()
    {
        using var factory = CreateFactory("Testing");
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", ForbiddenOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
