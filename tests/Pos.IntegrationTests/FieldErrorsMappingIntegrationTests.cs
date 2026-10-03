using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Pos.Api.Extensions;
using Pos.Domain.Common;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

// ── Controller temporal solo para el test ──────────────────────────────────

/// <summary>
/// Controller mínimo de prueba que expone ToActionResult con FieldErrors.
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
            ["Email"]  = ["Email es requerido", "Email inválido"],
            ["Nombre"] = ["Nombre es requerido"]
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
}

// ── Tests ──────────────────────────────────────────────────────────────────

/// <summary>
/// Verifica que MapErrorToActionResult serializa FieldErrors correctamente en el cuerpo RFC 9457.
/// Usa un controller temporal registrado solo en el contexto de test via AddApplicationPart.
/// </summary>
public class FieldErrorsMappingIntegrationTests
{
    private readonly WebApplicationFactory<Program> _factory;

    public FieldErrorsMappingIntegrationTests()
    {
        // Derivar de CustomWebApplicationFactory para heredar toda la configuración de infra/DB.
        // Añadir solo el ApplicationPart extra que contiene el controller temporal de test.
        _factory = new CustomWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Program.cs ya llamó AddControllers(), lo que registra ApplicationPartManager.
                    // Recuperamos el ApplicationPartManager del IServiceCollection para agregar
                    // el ensamblado de test como ApplicationPart sin necesitar AddControllers() aquí.
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

    [Fact]
    public async Task ValidationWithFieldErrors_Returns400_WithErrorsAsObjectInProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/_test/with-errors");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("errors", out var errorsEl).Should().BeTrue("debe incluir la clave 'errors'");
        errorsEl.ValueKind.Should().Be(JsonValueKind.Object, "errors debe ser un objeto, no un array");

        errorsEl.TryGetProperty("Email", out var emailErrors).Should().BeTrue();
        emailErrors.ValueKind.Should().Be(JsonValueKind.Array);
        emailErrors.GetArrayLength().Should().Be(2);

        errorsEl.TryGetProperty("Nombre", out var nombreErrors).Should().BeTrue();
        nombreErrors.ValueKind.Should().Be(JsonValueKind.Array);
        nombreErrors.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task ValidationWithoutFieldErrors_Returns400_WithoutErrorsKeyInProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/_test/without-errors");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Sin FieldErrors, no debe existir la clave "errors"
        root.TryGetProperty("errors", out _).Should().BeFalse(
            "el ProblemDetails no debe contener 'errors' cuando DomainError no tiene FieldErrors");
    }
}
