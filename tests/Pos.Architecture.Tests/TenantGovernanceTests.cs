using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pos.Architecture.Tests.Support;
using Xunit;

namespace Pos.Architecture.Tests;

public class TenantGovernanceTests
{
    private static CompilationUnitSyntax ParseSnippet(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
        var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, $"Snippet de prueba tiene errores de sintaxis:\n{string.Join("\n", errors.Select(e => e.GetMessage(System.Globalization.CultureInfo.InvariantCulture)))}");
        return tree.GetCompilationUnitRoot();
    }

    [Fact]
    public void NoApplicationCode_UsesTenantIdCoalesceGuidEmptyOrNewGuidForTenant()
    {
        var failures = new List<string>();

        foreach (var file in SourceTreeIndex.ApplicationAndApi.Files)
        {
            if (!file.FilePath.Contains("Pos.Application", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var findings = TenantFallbackScanner.Scan(file.Root, file.FilePath);
            foreach (var finding in findings)
            {
                var relPath = Path.GetRelativePath(SolutionDirectory.Root, file.FilePath);
                failures.Add($"- {relPath}:{finding.LineNumber} | Patrón: {finding.PatternDescription} | Expresión: '{finding.ExpressionText}'");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Prohibido usar fallbacks a Guid.Empty o Guid.NewGuid() para tenant en Pos.Application:\n{string.Join("\n", failures)}");
    }

    [Fact]
    public void AllCommandHandlers_InvokingRequireTenantId_HaveRequiresTenantOnCommand()
    {
        var failures = new List<string>();

        foreach (var file in SourceTreeIndex.ApplicationAndApi.Files)
        {
            if (!file.FilePath.Contains("Pos.Application", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var findings = HandlerTenantRequirementScanner.Scan(file.Root, file.FilePath);
            foreach (var finding in findings)
            {
                var relPath = Path.GetRelativePath(SolutionDirectory.Root, file.FilePath);
                failures.Add($"- {relPath} | {finding.Reason}");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Todo handler que invoca RequireTenantId() debe tener [RequiresTenant] en su comando:\n{string.Join("\n", failures)}");
    }

    [Fact]
    public void NoHandlerType_HasRequiresTenantAttribute()
    {
        var failures = new List<string>();

        foreach (var file in SourceTreeIndex.ApplicationAndApi.Files)
        {
            var findings = HandlerRequiresTenantAttributeScanner.Scan(file.Root, file.FilePath);
            foreach (var finding in findings)
            {
                var relPath = Path.GetRelativePath(SolutionDirectory.Root, file.FilePath);
                failures.Add($"- {relPath} | Handler: {finding.HandlerTypeName} | Atributo: [{finding.AttributeName}]");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"[RequiresTenant] y [AllowsNoTenant] están prohibidos en tipos *Handler (debe ir en el record del comando):\n{string.Join("\n", failures)}");
    }

    // --- Tests sintéticos para TenantFallbackScanner ---

    [Fact]
    public void TenantFallbackScanner_WhenTenantIdCoalescedWithGuidEmpty_DetectsViolation()
    {
        const string snippet = """
            namespace Sample;
            public class TestService
            {
                public void Execute(ICurrentTenantContext ctx)
                {
                    var id = ctx.TenantId ?? System.Guid.Empty;
                }
            }
            """;

        var root = ParseSnippet(snippet);
        var findings = TenantFallbackScanner.Scan(root, "Sample.cs");

        Assert.Single(findings);
        Assert.Equal("TenantId ?? Guid.Empty", findings[0].PatternDescription);
    }

    [Fact]
    public void TenantFallbackScanner_WhenTenantIdAssignedNewGuid_DetectsViolation()
    {
        const string snippet = """
            namespace Sample;
            public class TestService
            {
                public void Execute()
                {
                    System.Guid tenantId = System.Guid.NewGuid();
                }
            }
            """;

        var root = ParseSnippet(snippet);
        var findings = TenantFallbackScanner.Scan(root, "Sample.cs");

        Assert.Single(findings);
        Assert.Contains("Guid.NewGuid()", findings[0].PatternDescription);
    }

    [Fact]
    public void TenantFallbackScanner_WhenUsingRequireTenantId_DoesNotDetectViolation()
    {
        const string snippet = """
            namespace Sample;
            public class TestService
            {
                public void Execute(ICurrentTenantContext ctx)
                {
                    var id = ctx.RequireTenantId();
                }
            }
            """;

        var root = ParseSnippet(snippet);
        var findings = TenantFallbackScanner.Scan(root, "Sample.cs");

        Assert.Empty(findings);
    }

    // --- Tests sintéticos para HandlerTenantRequirementScanner ---

    [Fact]
    public void HandlerTenantRequirementScanner_WhenHandlerUsesRequireTenantIdWithoutAttributeOnCommand_DetectsViolation()
    {
        const string snippet = """
            namespace Sample;
            public record SampleCommand() : ICommand<Result>;

            public class SampleCommandHandler : ICommandHandler<SampleCommand, Result>
            {
                public async Task<Result> HandleAsync(SampleCommand cmd, CancellationToken ct)
                {
                    var id = _tenantContext.RequireTenantId();
                    return Result.Success();
                }
            }
            """;

        var root = ParseSnippet(snippet);
        var findings = HandlerTenantRequirementScanner.Scan(root, "Sample.cs");

        Assert.Single(findings);
        Assert.Equal("SampleCommandHandler", findings[0].HandlerTypeName);
        Assert.Equal("SampleCommand", findings[0].CommandTypeName);
    }

    [Fact]
    public void HandlerTenantRequirementScanner_WhenHandlerUsesRequireTenantIdWithAttributeOnCommand_DoesNotDetectViolation()
    {
        const string snippet = """
            namespace Sample;
            [RequiresTenant]
            public record SampleCommand() : ICommand<Result>;

            public class SampleCommandHandler : ICommandHandler<SampleCommand, Result>
            {
                public async Task<Result> HandleAsync(SampleCommand cmd, CancellationToken ct)
                {
                    var id = _tenantContext.RequireTenantId();
                    return Result.Success();
                }
            }
            """;

        var root = ParseSnippet(snippet);
        var findings = HandlerTenantRequirementScanner.Scan(root, "Sample.cs");

        Assert.Empty(findings);
    }

    // --- Tests sintéticos para HandlerRequiresTenantAttributeScanner ---

    [Fact]
    public void HandlerRequiresTenantAttributeScanner_WhenHandlerHasRequiresTenantAttribute_DetectsViolation()
    {
        const string snippet = """
            namespace Sample;
            [RequiresTenant]
            public class SampleCommandHandler
            {
            }
            """;

        var root = ParseSnippet(snippet);
        var findings = HandlerRequiresTenantAttributeScanner.Scan(root, "Sample.cs");

        Assert.Single(findings);
        Assert.Equal("SampleCommandHandler", findings[0].HandlerTypeName);
    }

    [Fact]
    public void HandlerRequiresTenantAttributeScanner_WhenCommandHasRequiresTenantAttribute_DoesNotDetectViolation()
    {
        const string snippet = """
            namespace Sample;
            [RequiresTenant]
            public record SampleCommand();

            public class SampleCommandHandler
            {
            }
            """;

        var root = ParseSnippet(snippet);
        var findings = HandlerRequiresTenantAttributeScanner.Scan(root, "Sample.cs");

        Assert.Empty(findings);
    }

    [Fact]
    public void HandlerRequiresTenantAttributeScanner_WhenHandlerHasAllowsNoTenantAttribute_DetectsViolation()
    {
        const string snippet = """
            namespace Sample;
            [AllowsNoTenant]
            public class SampleCommandHandler
            {
            }
            """;

        var root = ParseSnippet(snippet);
        var findings = HandlerRequiresTenantAttributeScanner.Scan(root, "Sample.cs");

        Assert.Single(findings);
        Assert.Equal("SampleCommandHandler", findings[0].HandlerTypeName);
    }

    // --- Tests de arquitectura y sintéticos para CommandTenantRequirementScanner ---

    [Fact]
    public void AllCommands_DeclareExactlyOne_Of_RequiresTenant_Or_AllowsNoTenant()
    {
        var failures = new List<string>();

        foreach (var file in SourceTreeIndex.ApplicationAndApi.Files)
        {
            if (!file.FilePath.Contains("Pos.Application", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var findings = CommandTenantRequirementScanner.Scan(file.Root, file.FilePath);
            foreach (var finding in findings)
            {
                var relPath = Path.GetRelativePath(SolutionDirectory.Root, file.FilePath);
                failures.Add($"- {relPath} | {finding.Reason}");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Todo comando (ICommand) debe declarar exactamente uno de [RequiresTenant] o [AllowsNoTenant] (Fail-Closed):\n{string.Join("\n", failures)}");
    }

    [Fact]
    public void CommandTenantRequirementScanner_WhenCommandHasRequiresTenant_DoesNotDetectViolation()
    {
        const string snippet = """
            namespace Sample;
            [RequiresTenant]
            public record SampleCommand() : ICommand<Result>;
            """;

        var root = ParseSnippet(snippet);
        var findings = CommandTenantRequirementScanner.Scan(root, "Sample.cs");

        Assert.Empty(findings);
    }

    [Fact]
    public void CommandTenantRequirementScanner_WhenCommandHasAllowsNoTenant_DoesNotDetectViolation()
    {
        const string snippet = """
            namespace Sample;
            [AllowsNoTenant]
            public record SampleCommand() : ICommand<Result>;
            """;

        var root = ParseSnippet(snippet);
        var findings = CommandTenantRequirementScanner.Scan(root, "Sample.cs");

        Assert.Empty(findings);
    }

    [Fact]
    public void CommandTenantRequirementScanner_WhenCommandHasNeither_DetectsViolation()
    {
        const string snippet = """
            namespace Sample;
            public record SampleCommand() : ICommand<Result>;
            """;

        var root = ParseSnippet(snippet);
        var findings = CommandTenantRequirementScanner.Scan(root, "Sample.cs");

        Assert.Single(findings);
        Assert.Equal("SampleCommand", findings[0].CommandTypeName);
        Assert.Contains("no declara ni [RequiresTenant] ni [AllowsNoTenant]", findings[0].Reason);
    }

    [Fact]
    public void CommandTenantRequirementScanner_WhenCommandHasBoth_DetectsViolation()
    {
        const string snippet = """
            namespace Sample;
            [RequiresTenant]
            [AllowsNoTenant]
            public record SampleCommand() : ICommand<Result>;
            """;

        var root = ParseSnippet(snippet);
        var findings = CommandTenantRequirementScanner.Scan(root, "Sample.cs");

        Assert.Single(findings);
        Assert.Equal("SampleCommand", findings[0].CommandTypeName);
        Assert.Contains("declara tanto [RequiresTenant] como [AllowsNoTenant]", findings[0].Reason);
    }

    // --- Tests de arquitectura y sintéticos para PlatformScopedEntityScanner ---

    [Fact]
    public void OnlyRoleAndAuditLog_CanImplement_IPlatformScopedEntity()
    {
        var failures = new List<string>();

        foreach (var file in SourceTreeIndex.Src.Files)
        {
            var findings = PlatformScopedEntityScanner.Scan(file.Root, file.FilePath);
            foreach (var finding in findings)
            {
                var relPath = Path.GetRelativePath(SolutionDirectory.Root, file.FilePath);
                failures.Add($"- {relPath} | {finding.Reason}");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Solo 'Role' y 'AuditLog' pueden implementar 'IPlatformScopedEntity' (lista cerrada según ADR 0008 y ADR 0017):\n{string.Join("\n", failures)}");
    }

    [Fact]
    public void PlatformScopedEntityScanner_WhenRoleImplementsInterface_DoesNotDetectViolation()
    {
        const string snippet = """
            namespace Sample;
            public class Role : IPlatformScopedEntity
            {
            }
            """;

        var root = ParseSnippet(snippet);
        var findings = PlatformScopedEntityScanner.Scan(root, "Role.cs");

        Assert.Empty(findings);
    }

    [Fact]
    public void PlatformScopedEntityScanner_WhenAuditLogImplementsInterface_DoesNotDetectViolation()
    {
        const string snippet = """
            namespace Sample;
            public class AuditLog : IPlatformScopedEntity
            {
            }
            """;

        var root = ParseSnippet(snippet);
        var findings = PlatformScopedEntityScanner.Scan(root, "AuditLog.cs");

        Assert.Empty(findings);
    }

    [Fact]
    public void PlatformScopedEntityScanner_WhenUnauthorizedEntityImplementsInterface_DetectsViolation()
    {
        const string snippet = """
            namespace Sample;
            public class Branch : IPlatformScopedEntity
            {
            }
            """;

        var root = ParseSnippet(snippet);
        var findings = PlatformScopedEntityScanner.Scan(root, "Branch.cs");

        Assert.Single(findings);
        Assert.Equal("Branch", findings[0].TypeName);
        Assert.Contains("solo 'Role' y 'AuditLog' están autorizados", findings[0].Reason);
    }

    // --- Tests de arquitectura y sintéticos para PlatformScopeSentinelScanner ---

    [Fact]
    public void GuidEmpty_IsForbiddenAsTenantSentinel_InInfrastructureAndApplication()
    {
        var failures = new List<string>();

        foreach (var file in SourceTreeIndex.Src.Files)
        {
            var relPath = Path.GetRelativePath(SolutionDirectory.Root, file.FilePath).Replace('\\', '/');

            // Limitar a Pos.Infrastructure y Pos.Application
            if (!relPath.StartsWith("src/Pos.Infrastructure/", StringComparison.OrdinalIgnoreCase) &&
                !relPath.StartsWith("src/Pos.Application/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Excluir migraciones generadas
            if (relPath.Contains("/Migrations/"))
            {
                continue;
            }

            var findings = PlatformScopeSentinelScanner.Scan(file.Root, file.FilePath);
            foreach (var finding in findings)
            {
                failures.Add($"- {relPath}:{finding.LineNumber} | {finding.PatternDescription} -> {finding.ExpressionText}");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"En 'src/Pos.Infrastructure' y 'src/Pos.Application' está prohibido el uso de 'Guid.Empty' como centinela de tenant. Debe usarse 'PlatformScope.TenantId' según ADR 0008 y ADR 0017:\n{string.Join("\n", failures)}");
    }

    [Fact]
    public void PlatformScopeSentinelScanner_WhenTenantIdCoalescedWithGuidEmpty_DetectsViolation()
    {
        const string snippet = """
            namespace Sample;
            public class TestClass
            {
                public void Method(Guid? tenantId)
                {
                    var id = tenantId ?? Guid.Empty;
                }
            }
            """;

        var root = ParseSnippet(snippet);
        var findings = PlatformScopeSentinelScanner.Scan(root, "TestClass.cs");

        Assert.Single(findings);
        Assert.Contains("Debe usar PlatformScope.TenantId", findings[0].PatternDescription);
    }

    [Fact]
    public void PlatformScopeSentinelScanner_WhenTenantIdComparedWithGuidEmpty_DetectsViolation()
    {
        const string snippet = """
            namespace Sample;
            public class TestClass
            {
                public bool Method(Guid tenantId)
                {
                    return tenantId == Guid.Empty;
                }
            }
            """;

        var root = ParseSnippet(snippet);
        var findings = PlatformScopeSentinelScanner.Scan(root, "TestClass.cs");

        Assert.Single(findings);
        Assert.Contains("Comparación de TenantId con Guid.Empty", findings[0].PatternDescription);
    }

    [Fact]
    public void PlatformScopeSentinelScanner_WhenNonTenantIdComparedWithGuidEmpty_DoesNotDetectViolation()
    {
        const string snippet = """
            namespace Sample;
            public class TestClass
            {
                public bool Method(Guid productId)
                {
                    return productId == Guid.Empty;
                }
            }
            """;

        var root = ParseSnippet(snippet);
        var findings = PlatformScopeSentinelScanner.Scan(root, "TestClass.cs");

        Assert.Empty(findings);
    }

    [Fact]
    public void PlatformScopeSentinelScanner_WhenUsingPlatformScopeTenantId_DoesNotDetectViolation()
    {
        const string snippet = """
            namespace Sample;
            public class TestClass
            {
                public bool Method(Guid tenantId)
                {
                    return tenantId == PlatformScope.TenantId;
                }
            }
            """;

        var root = ParseSnippet(snippet);
        var findings = PlatformScopeSentinelScanner.Scan(root, "TestClass.cs");

        Assert.Empty(findings);
    }

    [Fact]
    public void TenantEntityFactories_MustRequireTenantIdParameter_ExceptRatchetList()
    {
        var domainDir = Path.Combine(SolutionDirectory.Root, "src", "Pos.Domain");
        var domainIndex = SourceTreeIndex.ForDirectory(domainDir);
        domainIndex.EnsureNoSyntaxErrors();

        var nonCompliantEntitiesFound = new HashSet<string>(StringComparer.Ordinal);
        var unallowedViolations = new List<string>();

        foreach (var file in domainIndex.Files)
        {
            var findings = TenantEntityFactoryRatchetScanner.Scan(file.Root, file.FilePath);
            foreach (var finding in findings)
            {
                nonCompliantEntitiesFound.Add(finding.EntityName);

                if (!TenantEntityFactoryRatchetScanner.PendingEntities.Contains(finding.EntityName))
                {
                    var relPath = Path.GetRelativePath(SolutionDirectory.Root, finding.FilePath);
                    unallowedViolations.Add($"- {relPath}:{finding.LineNumber} | {finding.EntityName}::{finding.MethodSignature} | {finding.Description}");
                }
            }
        }

        // 1. Ninguna entidad fuera de la lista de pendientes puede exponer factorías Create* sin Guid tenantId
        Assert.True(
            unallowedViolations.Count == 0,
            $"Factorías de entidad de tenant sin parámetro 'Guid tenantId' fuera de la lista ratchet de pendientes:\n{string.Join("\n", unallowedViolations)}");

        // 2. Ratchet hacia adelante: si una entidad de la lista ya no tiene factorías no conformes, la lista debe encogerse
        var obsoletePending = TenantEntityFactoryRatchetScanner.PendingEntities
            .Where(e => !nonCompliantEntitiesFound.Contains(e))
            .ToList();

        Assert.True(
            obsoletePending.Count == 0,
            $"Entrada(s) obsoleta(s) en la lista ratchet de factorías pendientes (ya cumplen la regla y deben retirarse de PendingEntities):\n{string.Join(", ", obsoletePending)}");

        // 3. Cierre definitivo de ratchet: 100% de entidades de tenant gobernadas; prohibido reabrir o añadir nuevas excepciones
        Assert.Empty(TenantEntityFactoryRatchetScanner.PendingEntities);
    }

    [Fact]
    public void TenantEntityFactoryRatchetScanner_WhenFactoryLacksTenantId_DetectsViolation()
    {
        const string snippet = """
            namespace Sample;
            public class SampleEntity : Pos.Domain.Common.AggregateRoot<System.Guid>, Pos.Domain.Common.ITenantOwnedEntity
            {
                public System.Guid TenantId { get; private set; }
                public static SampleEntity Create(string name) => new SampleEntity();
            }
            """;

        var root = ParseSnippet(snippet);
        var findings = TenantEntityFactoryRatchetScanner.Scan(root, "SampleEntity.cs");

        Assert.Single(findings);
        Assert.Equal("SampleEntity", findings[0].EntityName);
        Assert.Contains("Guid tenantId", findings[0].Description);
    }

    [Fact]
    public void TenantEntityFactoryRatchetScanner_WhenFactoryHasTenantId_DoesNotDetectViolation()
    {
        const string snippet = """
            namespace Sample;
            public class SampleEntity : Pos.Domain.Common.AggregateRoot<System.Guid>, Pos.Domain.Common.ITenantOwnedEntity
            {
                public System.Guid TenantId { get; private set; }
                public static SampleEntity Create(System.Guid tenantId, string name) => new SampleEntity();
            }
            """;

        var root = ParseSnippet(snippet);
        var findings = TenantEntityFactoryRatchetScanner.Scan(root, "SampleEntity.cs");

        Assert.Empty(findings);
    }

    [Fact]
    public void TenantEntityFactoryRatchetScanner_WhenPlatformScoped_IgnoresFactoryWithoutTenantId()
    {
        const string snippet = """
            namespace Sample;
            public class PlatformEntity : Pos.Domain.Common.AggregateRoot<System.Guid>, Pos.Domain.Common.ITenantOwnedEntity, Pos.Domain.Common.IPlatformScopedEntity
            {
                public System.Guid TenantId { get; private set; }
                public static PlatformEntity Create(string name) => new PlatformEntity();
            }
            """;

        var root = ParseSnippet(snippet);
        var findings = TenantEntityFactoryRatchetScanner.Scan(root, "PlatformEntity.cs");

        Assert.Empty(findings);
    }

    [Fact]
    public void Only_Authorized_Ports_And_Platform_Components_May_Invoke_IgnoreQueryFilters()
    {
        // Lista CERRADA y sellada de componentes de infraestructura autorizados para omitir filtros de consulta globales.
        // Queda estrictamente proscrito en cualquier repositorio de negocio (*Repository.cs).
        var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AuthUserLookup.cs",
            "SuperAdminSeeder.cs",
            "PosDbContext.cs",
            "PendingInvoicePlatformReader.cs"
        };

        var violations = new List<string>();

        foreach (var file in SourceTreeIndex.Src.Files)
        {
            var fileName = Path.GetFileName(file.FilePath);
            var isRepository = fileName.EndsWith("Repository.cs", StringComparison.OrdinalIgnoreCase);

            var invocations = file.Root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => inv.Expression is MemberAccessExpressionSyntax memberAccess &&
                              memberAccess.Name.Identifier.ValueText == "IgnoreQueryFilters")
                .ToList();

            if (invocations.Count > 0)
            {
                var relPath = Path.GetRelativePath(SolutionDirectory.Root, file.FilePath);

                if (isRepository)
                {
                    violations.Add($"PROHIBICIÓN ESTRICTA: El repositorio '{relPath}' invoca 'IgnoreQueryFilters()'. Toda consulta global debe residir en puertos autorizados de plataforma (ej. IAuthUserLookup).");
                }
                else if (!allowedFiles.Contains(fileName))
                {
                    violations.Add($"ARCHIVO NO AUTORIZADO: '{relPath}' invoca 'IgnoreQueryFilters()'. Solo los archivos de la lista cerrada autorizada tienen permitido su uso: [{string.Join(", ", allowedFiles)}].");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Violaciones a la gobernanza de IgnoreQueryFilters() detectadas:\n{string.Join("\n", violations)}");
    }
}




