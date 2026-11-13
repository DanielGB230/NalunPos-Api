using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

public class OpenApiContractSnapshotTests : IClassFixture<OpenApiTestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions s_serializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly OpenApiTestWebApplicationFactory _factory;

    public OpenApiContractSnapshotTests(OpenApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenApiDocument_MatchesVersionedSnapshot_WithoutContractDrift()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "El endpoint OpenAPI /openapi/v1.json debe responder 200 OK.");
        var rawJson = await response.Content.ReadAsStringAsync();

        var normalizedCurrentJson = NormalizeOpenApiJson(rawJson);
        var snapshotPath = GetSnapshotFilePath();

        var updateSnapshotEnv = Environment.GetEnvironmentVariable("UPDATE_OPENAPI_SNAPSHOT");
        bool shouldUpdate = string.Equals(updateSnapshotEnv, "true", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(updateSnapshotEnv, "1", StringComparison.OrdinalIgnoreCase);

        if (shouldUpdate)
        {
            var dir = Path.GetDirectoryName(snapshotPath)!;
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(snapshotPath, normalizedCurrentJson);
        }

        File.Exists(snapshotPath).Should().BeTrue(
            $"El archivo de snapshot OpenAPI '{snapshotPath}' debe existir. " +
            "Ejecute con UPDATE_OPENAPI_SNAPSHOT=true para generarlo si es una actualización de contrato autorizada.");

        var expectedSnapshotJson = await File.ReadAllTextAsync(snapshotPath);
        expectedSnapshotJson = expectedSnapshotJson.Replace("\r\n", "\n");

        var driftMessage = ComputeDriftDifferenceMessage(expectedSnapshotJson, normalizedCurrentJson);
        driftMessage.Should().BeNull(
            "Se detectó deriva de contrato en OpenAPI v1 respecto al snapshot versionado en docs/api/openapi.v1.json.\n" +
            driftMessage + "\n\n" +
            "Si este cambio de contrato es intencional y autorizado, actualice el snapshot ejecutando:\n" +
            "UPDATE_OPENAPI_SNAPSHOT=true dotnet test tests/Pos.IntegrationTests/Pos.IntegrationTests.csproj --filter FullyQualifiedName~OpenApiContractSnapshotTests\n");
    }

    [Fact]
    public void ContractDriftDetector_WhenContractIsAltered_DetectsDiscrepancyAndInstructsUpdate()
    {
        // Prueba negativa: Alterar en memoria una ruta/propiedad y comprobar que el comparador detecta la deriva
        const string baselineJson = """
            {
              "openapi": "3.1.0",
              "paths": {
                "/api/v1/auth/login": {
                  "post": {
                    "summary": "Login"
                  }
                }
              }
            }
            """;

        const string mutatedJson = """
            {
              "openapi": "3.1.0",
              "paths": {
                "/api/v1/auth/login": {
                  "post": {
                    "summary": "LoginModificadoSinAutorizacion"
                  }
                }
              }
            }
            """;

        var diff = ComputeDriftDifferenceMessage(
            NormalizeOpenApiJson(baselineJson),
            NormalizeOpenApiJson(mutatedJson));

        diff.Should().NotBeNull("El detector de deriva debe reportar diferencias cuando el contrato cambia.");
        diff.Should().Contain("paths./api/v1/auth/login.post.summary");
    }

    [Fact]
    public async Task OpenApiAndScalarEndpoints_WhenOutsideDevelopmentEnvironment_AreNotExposed()
    {
        using var standardFactory = new CustomWebApplicationFactory();
        var client = standardFactory.CreateClient();

        var openApiResponse = await client.GetAsync("/openapi/v1.json");
        var scalarResponse = await client.GetAsync("/scalar");

        openApiResponse.StatusCode.Should().Match(s => s == HttpStatusCode.NotFound || s == HttpStatusCode.Unauthorized,
            "El endpoint /openapi/v1.json no debe exponerse fuera del entorno Development.");
        scalarResponse.StatusCode.Should().Match(s => s == HttpStatusCode.NotFound || s == HttpStatusCode.Unauthorized,
            "La consola /scalar no debe exponerse fuera del entorno Development.");

        var testClient = _factory.CreateClient();
        var testResponse = await testClient.GetAsync("/openapi/v1.json");
        testResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "El host OpenApiTestWebApplicationFactory debe exponer el documento para pruebas de contrato.");
    }

    [Fact]
    public async Task OpenApiDocument_ContainsExpectedCanonicalSchemas_AndSecurityDefinitions()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var rawJson = await response.Content.ReadAsStringAsync();
        var root = JsonNode.Parse(rawJson)!.AsObject();

        // 1. Bearer Security Scheme
        root["components"]?["securitySchemes"]?["Bearer"].Should().NotBeNull("El esquema de seguridad Bearer debe estar configurado.");
        root["components"]!["securitySchemes"]!["Bearer"]!["scheme"]?.GetValue<string>().ToLowerInvariant()
            .Should().Be("bearer");

        // 2. Schemas canónicos exportados
        var schemas = root["components"]?["schemas"]?.AsObject();
        schemas.Should().NotBeNull();

        // LoginResponse y LoginRequest
        schemas!["LoginResponse"].Should().NotBeNull("LoginResponse debe estar documentado en components.schemas.");
        var loginResponseProps = schemas["LoginResponse"]!["properties"]?.AsObject();
        loginResponseProps.Should().NotBeNull();
        loginResponseProps!["token"].Should().NotBeNull();
        loginResponseProps["role"].Should().NotBeNull();
        loginResponseProps["userId"].Should().NotBeNull();
        loginResponseProps["email"].Should().NotBeNull();

        // WarehouseDto y CreateWarehouseRequest
        schemas["WarehouseDto"].Should().NotBeNull("WarehouseDto debe estar documentado en components.schemas.");
        schemas["CreateWarehouseRequest"].Should().NotBeNull("CreateWarehouseRequest debe estar documentado en components.schemas.");

        // ProblemDetails con extensión "code"
        schemas["ProblemDetails"].Should().NotBeNull("ProblemDetails debe estar documentado en components.schemas.");
        var problemProps = schemas["ProblemDetails"]!["properties"]?.AsObject();
        problemProps.Should().NotBeNull();
        problemProps!["code"].Should().NotBeNull("El schema ProblemDetails debe incluir la propiedad 'code' (ADR 0023).");

        // 3. Enums serializados como string
        var enumSchema = schemas["AgentActionStatus"]?.AsObject();
        enumSchema.Should().NotBeNull("AgentActionStatus enum debe estar en components.schemas.");
        var enumValues = enumSchema!["enum"]?.AsArray();
        enumValues.Should().NotBeNull("Los enums deben serializarse con nombres en string.");
        enumValues!.Select(v => v?.GetValue<string>()).Should().Contain("PendingApproval");
    }

    public static string NormalizeOpenApiJson(string json)
    {
        var node = JsonNode.Parse(json);
        var normalizedNode = SortAndNormalizeNode(node);

        var result = JsonSerializer.Serialize(normalizedNode, s_serializerOptions);
        // Normalizar saltos de línea a \n
        return result.Replace("\r\n", "\n") + "\n";
    }

    private static JsonNode? SortAndNormalizeNode(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var sortedObj = new JsonObject();
            var sortedProperties = obj
                .Where(kvp => !string.Equals(kvp.Key, "servers", StringComparison.OrdinalIgnoreCase))
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .ToList();

            foreach (var kvp in sortedProperties)
            {
                sortedObj.Add(kvp.Key, SortAndNormalizeNode(kvp.Value?.DeepClone()));
            }
            return sortedObj;
        }

        if (node is JsonArray arr)
        {
            var sortedArr = new JsonArray();
            foreach (var item in arr)
            {
                sortedArr.Add(SortAndNormalizeNode(item?.DeepClone()));
            }
            return sortedArr;
        }

        return node?.DeepClone();
    }

    public static string? ComputeDriftDifferenceMessage(string expectedJson, string actualJson)
    {
        var expectedNode = JsonNode.Parse(expectedJson);
        var actualNode = JsonNode.Parse(actualJson);

        var differences = new List<string>();
        CompareNodes("$", expectedNode, actualNode, differences);

        if (differences.Count == 0) return null;

        return string.Join("\n", differences.Take(15).Select(d => "  - " + d)) +
               (differences.Count > 15 ? $"\n  ... y {differences.Count - 15} diferencias adicionales." : "");
    }

    private static void CompareNodes(string path, JsonNode? expected, JsonNode? actual, List<string> differences)
    {
        if (expected == null && actual == null) return;
        if (expected == null || actual == null)
        {
            differences.Add($"En '{path}': esperado {(expected == null ? "null" : "definido")}, pero obtenido {(actual == null ? "null" : "definido")}");
            return;
        }

        if (expected.GetValueKind() != actual.GetValueKind())
        {
            differences.Add($"En '{path}': tipo esperado '{expected.GetValueKind()}', pero obtenido '{actual.GetValueKind()}'");
            return;
        }

        if (expected is JsonObject expObj && actual is JsonObject actObj)
        {
            var expKeys = expObj.Select(k => k.Key).ToHashSet(StringComparer.Ordinal);
            var actKeys = actObj.Select(k => k.Key).ToHashSet(StringComparer.Ordinal);

            foreach (var missingKey in expKeys.Except(actKeys))
            {
                differences.Add($"En '{path}': propiedad eliminada o faltante '{missingKey}'");
            }

            foreach (var extraKey in actKeys.Except(expKeys))
            {
                differences.Add($"En '{path}': propiedad nueva no registrada '{extraKey}'");
            }

            foreach (var commonKey in expKeys.Intersect(actKeys))
            {
                CompareNodes($"{path}.{commonKey}", expObj[commonKey], actObj[commonKey], differences);
            }
            return;
        }

        if (expected is JsonArray expArr && actual is JsonArray actArr)
        {
            if (expArr.Count != actArr.Count)
            {
                differences.Add($"En '{path}': longitud de arreglo esperada {expArr.Count}, pero obtenida {actArr.Count}");
            }
            int minLen = Math.Min(expArr.Count, actArr.Count);
            for (int i = 0; i < minLen; i++)
            {
                CompareNodes($"{path}[{i}]", expArr[i], actArr[i], differences);
            }
            return;
        }

        var expVal = expected.ToJsonString();
        var actVal = actual.ToJsonString();
        if (!string.Equals(expVal, actVal, StringComparison.Ordinal))
        {
            differences.Add($"En '{path}': valor esperado {expVal}, pero obtenido {actVal}");
        }
    }

    private static string GetSnapshotFilePath()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "Pos.slnx")))
        {
            currentDir = currentDir.Parent;
        }

        if (currentDir == null)
        {
            throw new InvalidOperationException("No se encontró la raíz del repositorio que contiene Pos.slnx.");
        }

        return Path.Combine(currentDir.FullName, "docs", "api", "openapi.v1.json");
    }
}
