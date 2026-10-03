using System.Net;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.Testing;
using Pos.Api.Common;
using Pos.Api.Extensions;
using Pos.Domain.Common;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

// ── Controller temporal solo para el test ──────────────────────────────────

/// <summary>
/// Controller mínimo de prueba que expone ToActionResult y lanza ValidationException.
/// Solo existe en el contexto de test — no es parte del ensamblado de producción.
/// </summary>
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
[Microsoft.AspNetCore.Mvc.ApiController]
[Microsoft.AspNetCore.Mvc.Route("_test")]
public sealed class FieldErrorsTestController : Microsoft.AspNetCore.Mvc.ControllerBase
{
    [Microsoft.AspNetCore.Mvc.HttpGet("with-errors")]
    public Microsoft.AspNetCore.Mvc.IActionResult WithErrors()
    {
        var fieldErrors = new FieldErrors(new Dictionary<string, string[]>
        {
            ["Email"] = ["Email es requerido", "Email inválido"],
            ["Items[0].Quantity"] = ["Cantidad debe ser mayor a 0"]
        });

        var error = DomainError.Validation("VAL.MultiField", "Errores de validación.", fieldErrors);
        Result<string> result = error;
        return this.ToActionResult(result);
    }

    [Microsoft.AspNetCore.Mvc.HttpGet("without-errors")]
    public Microsoft.AspNetCore.Mvc.IActionResult WithoutErrors()
    {
        var error = DomainError.Validation("VAL.Simple", "Error simple sin campos.");
        Result<string> result = error;
        return this.ToActionResult(result);
    }

    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [Microsoft.AspNetCore.Mvc.HttpGet("throw-validation-exception")]
    public Microsoft.AspNetCore.Mvc.IActionResult ThrowValidationException()
    {
        _ = this.HttpContext;
        throw new ValidationException(new[]
        {
            new ValidationFailure("UserEmail", "Email de usuario es requerido")
        });
    }

    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [Microsoft.AspNetCore.Mvc.HttpGet("key-collision")]
    public Microsoft.AspNetCore.Mvc.IActionResult KeyCollision()
    {
        var fieldErrors = new FieldErrors(new Dictionary<string, string[]>
        {
            ["Email"] = ["Error Mayúscula"],
            ["email"] = ["Error Minúscula"]
        });

        var error = DomainError.Validation("VAL.Collision", "Colisión de claves.", fieldErrors);
        Result<string> result = error;
        return this.ToActionResult(result);
    }
}

// ── Tests ──────────────────────────────────────────────────────────────────

/// <summary>
/// Verifica el contrato JSON unificado de ProblemDetails (RFC 9110 / RFC 9457) desde los tres orígenes:
/// 1. Result fallido con FieldErrors vía controller
/// 2. ValidationException vía middleware
/// 3. Rate limiting (429)
/// </summary>
public class FieldErrorsMappingIntegrationTests
{
    private readonly WebApplicationFactory<Program> _factory;

    public FieldErrorsMappingIntegrationTests()
    {
        _factory = new CustomWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var partManagerDescriptor = services.FirstOrDefault(
                        sd => sd.ServiceType == typeof(Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartManager));

                    if (partManagerDescriptor?.ImplementationInstance is Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartManager partManager)
                    {
                        var testAssemblyPart = new Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart(
                            typeof(FieldErrorsTestController).Assembly);

                        if (!partManager.ApplicationParts.OfType<Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart>()
                                .Any(p => p.Assembly == typeof(FieldErrorsTestController).Assembly))
                        {
                            partManager.ApplicationParts.Add(testAssemblyPart);
                        }
                    }
                });
            });
    }

    private static void VerifyBaseProblemDetailsShape(JsonElement root, int expectedStatus)
    {
        root.TryGetProperty("type", out var typeEl).Should().BeTrue();
        typeEl.GetString().Should().NotBeNullOrEmpty();

        root.TryGetProperty("title", out var titleEl).Should().BeTrue();
        titleEl.GetString().Should().NotBeNullOrEmpty();

        root.TryGetProperty("status", out var statusEl).Should().BeTrue();
        statusEl.GetInt32().Should().Be(expectedStatus);

        root.TryGetProperty("correlationId", out var correlationEl).Should().BeTrue();
        correlationEl.GetString().Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("ID", "id")]
    [InlineData("URL", "url")]
    [InlineData("UserId", "userId")]
    [InlineData("Items[0].Quantity", "items[0].quantity")]
    [InlineData("", "")]
    public void ToCamelCasePropertyPath_FormatsKeysMatchingSystemTextJsonPolicy(string input, string expected)
    {
        var result = PosProblemDetailsFactory.ToCamelCasePropertyPath(input);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task KeyCollision_MergesMessageArraysInOrderWithoutOverwriting()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/_test/key-collision");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("errors", out var errorsEl).Should().BeTrue();
        errorsEl.TryGetProperty("email", out var emailArray).Should().BeTrue();

        emailArray.GetArrayLength().Should().Be(2);
        emailArray[0].GetString().Should().Be("Error Mayúscula");
        emailArray[1].GetString().Should().Be("Error Minúscula");
    }

    [Fact]
    public async Task Origin1_ResultWithFieldErrors_Returns400_WithCamelCaseErrorsKeyInProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/_test/with-errors");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        VerifyBaseProblemDetailsShape(root, 400);

        root.TryGetProperty("errors", out var errorsEl).Should().BeTrue("debe incluir la clave 'errors'");
        errorsEl.ValueKind.Should().Be(JsonValueKind.Object);

        errorsEl.TryGetProperty("email", out var emailErrors).Should().BeTrue("Email debe estar en camelCase ('email')");
        emailErrors.GetArrayLength().Should().Be(2);

        errorsEl.TryGetProperty("items[0].quantity", out var itemQuantityErrors).Should().BeTrue("Items[0].Quantity debe ser 'items[0].quantity'");
        itemQuantityErrors.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Origin2_ValidationExceptionViaMiddleware_Returns400_WithCamelCaseErrorsRecord()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/_test/throw-validation-exception");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        VerifyBaseProblemDetailsShape(root, 400);

        root.TryGetProperty("errors", out var errorsEl).Should().BeTrue("middleware debe incluir la clave 'errors'");
        errorsEl.ValueKind.Should().Be(JsonValueKind.Object, "errors debe ser un objeto Record, no un array");

        errorsEl.TryGetProperty("userEmail", out var emailErrors).Should().BeTrue("UserEmail debe formatearse como userEmail");
        emailErrors.ValueKind.Should().Be(JsonValueKind.Array);
        emailErrors.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Origin3_RateLimiting429_ReturnsUnifiedProblemDetailsShape_WithoutErrorsKey()
    {
        var client = _factory.CreateClient();

        HttpResponseMessage response = null!;
        for (int i = 0; i < 15; i++)
        {
            response = await client.PostAsync("/api/v1/auth/login", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                break;
        }

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        VerifyBaseProblemDetailsShape(root, 429);

        root.TryGetProperty("errors", out _).Should().BeFalse("respuesta 429 no debe incluir la clave 'errors'");
    }

    [Fact]
    public async Task ResultWithoutFieldErrors_Returns400_WithoutErrorsKeyInProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/_test/without-errors");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        VerifyBaseProblemDetailsShape(root, 400);

        root.TryGetProperty("errors", out _).Should().BeFalse(
            "el ProblemDetails no debe contener 'errors' cuando no hay FieldErrors");
    }
}
