using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Architecture.Tests.Support;
using Pos.Domain.Common;
using Pos.Infrastructure;
using Xunit;

namespace Pos.Architecture.Tests;

public class ArchitectureTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture =
        new ArchLoader().LoadAssemblies(
            typeof(Entity<>).Assembly,               // Pos.Domain
            typeof(IEventBus).Assembly,             // Pos.Application
            typeof(DependencyInjection).Assembly,   // Pos.Infrastructure
            typeof(Program).Assembly                // Pos.Api
        ).Build();

    private static readonly IObjectProvider<IType> DomainLayer =
        ArchRuleDefinition.Types().That().ResideInAssembly(typeof(Entity<>).Assembly).As("Domain Layer");

    private static readonly IObjectProvider<IType> ApplicationLayer =
        ArchRuleDefinition.Types().That().ResideInAssembly(typeof(IEventBus).Assembly).As("Application Layer");

    private static readonly IObjectProvider<IType> InfrastructureLayer =
        ArchRuleDefinition.Types().That().ResideInAssembly(typeof(DependencyInjection).Assembly).As("Infrastructure Layer");

    [Fact]
    public void Domain_ShouldNot_DependOn_Infrastructure()
    {
        IArchRule rule = ArchRuleDefinition.Types()
            .That().Are(DomainLayer)
            .Should().NotDependOnAny(InfrastructureLayer);

        rule.Check(Architecture);
    }

    [Fact]
    public void Domain_ShouldNot_DependOn_EntityFrameworkCore()
    {
        IArchRule rule = ArchRuleDefinition.Types()
            .That().Are(DomainLayer)
            .Should().NotDependOnAny("Microsoft.EntityFrameworkCore", true);

        rule.Check(Architecture);
    }

    [Fact]
    public void Domain_ShouldNot_DependOn_AspNetCore()
    {
        IArchRule rule = ArchRuleDefinition.Types()
            .That().Are(DomainLayer)
            .Should().NotDependOnAny("Microsoft.AspNetCore", true);

        rule.Check(Architecture);
    }

    [Fact]
    public void Application_ShouldNot_DependOn_Infrastructure()
    {
        IArchRule rule = ArchRuleDefinition.Types()
            .That().Are(ApplicationLayer)
            .Should().NotDependOnAny(InfrastructureLayer);

        rule.Check(Architecture);
    }

    [Fact]
    public void Application_ShouldNot_DependOn_EntityFrameworkCore()
    {
        IArchRule rule = ArchRuleDefinition.Types()
            .That().Are(ApplicationLayer)
            .Should().NotDependOnAny("Microsoft.EntityFrameworkCore", true);

        rule.Check(Architecture);
    }

    [Fact]
    public void Application_ShouldNot_DependOn_AspNetCore()
    {
        IArchRule rule = ArchRuleDefinition.Types()
            .That().Are(ApplicationLayer)
            .Should().NotDependOnAny("Microsoft.AspNetCore", true);

        rule.Check(Architecture);
    }

    [Fact]
    public void IntegrationEventContracts_ShouldNot_DependOn_Infrastructure()
    {
        IObjectProvider<IType> integrationEventContracts = ArchRuleDefinition.Types()
            .That().ResideInNamespace("Pos.Application.IntegrationEvents.Contracts")
            .As("Integration Event Contracts");

        IArchRule rule = ArchRuleDefinition.Types()
            .That().Are(integrationEventContracts)
            .Should().NotDependOnAny(InfrastructureLayer);

        rule.Check(Architecture);
    }

    [Fact]
    public void ITenantOwnedEntity_ShouldResideIn_DomainCommon()
    {
        IObjectProvider<IType> tenantOwnedEntityInterface = ArchRuleDefinition.Types()
            .That().Are(typeof(ITenantOwnedEntity))
            .As("ITenantOwnedEntity Interface");

        IArchRule rule = ArchRuleDefinition.Types()
            .That().Are(tenantOwnedEntityInterface)
            .Should().ResideInNamespace("Pos.Domain.Common");

        rule.Check(Architecture);
    }

    [Fact]
    public void ICurrentTenantContext_ShouldResideIn_ApplicationCommonInterfaces()
    {
        IObjectProvider<IType> currentTenantContextInterface = ArchRuleDefinition.Types()
            .That().Are(typeof(ICurrentTenantContext))
            .As("ICurrentTenantContext Interface");

        IArchRule rule = ArchRuleDefinition.Types()
            .That().Are(currentTenantContextInterface)
            .Should().ResideInNamespace("Pos.Application.Common.Interfaces");

        rule.Check(Architecture);
    }

    [Fact]
    public void ConcreteIntegrationEvents_ShouldResideIn_VersionedNamespace()
    {
        IObjectProvider<IType> concreteIntegrationEvents = ArchRuleDefinition.Types()
            .That().ResideInNamespace("Pos.Application.IntegrationEvents.Contracts", true)
            .And().AreNot(typeof(Pos.Application.IntegrationEvents.Contracts.IIntegrationEvent))
            .As("Concrete Integration Events");

        IArchRule rule = ArchRuleDefinition.Types()
            .That().Are(concreteIntegrationEvents)
            .Should().ResideInNamespace(@"Pos\.Application\.IntegrationEvents\.Contracts\.V[0-9]+.*", true);

        rule.Check(Architecture);
    }

    [Fact]
    public void All_Entity_Identifiers_And_ForeignKeys_Must_Use_Guid()
    {
        var domainAssembly = typeof(Entity<>).Assembly;
        var entityTypes = domainAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && (
                IsSubclassOfRawGeneric(typeof(Entity<>), t) || 
                IsSubclassOfRawGeneric(typeof(AggregateRoot<>), t)))
            .ToList();

        Assert.NotEmpty(entityTypes);

        foreach (var entityType in entityTypes)
        {
            // Primary Key (Id) must strictly be Guid
            var pkProperty = entityType.GetProperty("Id");
            Assert.NotNull(pkProperty);
            Assert.True(
                pkProperty.PropertyType == typeof(Guid),
                $"La llave primaria 'Id' de la entidad '{entityType.Name}' debe ser de tipo Guid (UUIDv7)."
            );

            // All properties ending with 'Id' must NOT be integer/numeric types (int, long, etc.)
            var idProperties = entityType.GetProperties()
                .Where(p => p.Name.EndsWith("Id", StringComparison.Ordinal))
                .ToList();

            foreach (var prop in idProperties)
            {
                var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                Assert.False(
                    propType == typeof(int) || propType == typeof(long) || propType == typeof(short) || propType == typeof(byte),
                    $"La propiedad '{prop.Name}' en la entidad '{entityType.Name}' usa el tipo numérico '{prop.PropertyType.Name}'. El uso de IDs numéricos (int/long) para PKs o FKs está estrictamente prohibido."
                );
            }
        }
    }

    private static bool IsSubclassOfRawGeneric(System.Type generic, System.Type? toCheck)
    {
        while (toCheck != null && toCheck != typeof(object))
        {
            var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
            if (generic == cur)
            {
                return true;
            }
            toCheck = toCheck.BaseType;
        }
        return false;
    }

    [Fact]
    public void All_Business_Entities_Must_Implement_ITenantOwnedEntity()
    {
        var domainAssembly = typeof(Entity<>).Assembly;
        var businessEntityTypes = domainAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract &&
                t.Namespace == "Pos.Domain.Entities" &&
                t.Name != "Tenant" && t.Name != "User" &&
                !t.Name.EndsWith("Line", StringComparison.Ordinal) && !t.Name.EndsWith("LineItem", StringComparison.Ordinal) &&
                (IsSubclassOfRawGeneric(typeof(Entity<>), t) || IsSubclassOfRawGeneric(typeof(AggregateRoot<>), t)))
            .ToList();

        Assert.NotEmpty(businessEntityTypes);

        foreach (var entityType in businessEntityTypes)
        {
            Assert.True(
                typeof(ITenantOwnedEntity).IsAssignableFrom(entityType),
                $"La entidad de negocio '{entityType.Name}' debe implementar la interfaz 'ITenantOwnedEntity' para garantizar el aislamiento multi-tenant."
            );
        }
    }

    [Fact]
    public void Controllers_MustNot_Use_HttpPut_Or_HttpDelete_Attributes()
    {
        var apiAssembly = typeof(Program).Assembly;
        var controllerTypes = apiAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t))
            .ToList();

        Assert.NotEmpty(controllerTypes);

        foreach (var controller in controllerTypes)
        {
            var methods = controller.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                var hasHttpPut = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPutAttribute), true).Length > 0;
                var hasHttpDelete = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpDeleteAttribute), true).Length > 0;

                Assert.False(hasHttpPut, $"El método '{method.Name}' en '{controller.Name}' utiliza [HttpPut]. Se debe usar [HttpPatch].");
                Assert.False(hasHttpDelete, $"El método '{method.Name}' en '{controller.Name}' utiliza [HttpDelete]. Se debe usar [HttpPatch] para soft delete.");
            }
        }
    }

    [Fact]
    public void All_Entities_With_TenantId_Property_Must_Have_Global_Query_Filter_In_EF_Model()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Pos.Infrastructure.Persistence.Context.PosDbContext>()
            .UseInMemoryDatabase("GovernanceArchitectureTestDb")
            .Options;

        using var dbContext = new Pos.Infrastructure.Persistence.Context.PosDbContext(options, currentTenantId: Guid.NewGuid());
        var entityTypes = dbContext.Model.GetEntityTypes();

        var whitelistedEntityNames = new HashSet<string>
        {
            "Tenant",
            "OutboxMessage"
        };

        var missingFilterEntities = new List<string>();

        foreach (var entityType in entityTypes)
        {
            if (entityType.IsOwned()) continue;

            var clrType = entityType.ClrType;
            if (clrType == null) continue;

            if (whitelistedEntityNames.Contains(clrType.Name)) continue;

            var hasTenantIdProperty = clrType.GetProperty("TenantId") != null;
            if (hasTenantIdProperty)
            {
#pragma warning disable CS0618
                var queryFilter = entityType.GetQueryFilter();
#pragma warning restore CS0618
                if (queryFilter == null)
                {
                    missingFilterEntities.Add(clrType.Name);
                }
            }
        }

        Assert.True(
            missingFilterEntities.Count == 0,
            $"Las siguientes entidades contienen la propiedad 'TenantId' pero carecen de un Global Query Filter en PosDbContext: {string.Join(", ", missingFilterEntities)}"
        );
    }

    [Fact]
    public void All_CommandHandlers_And_QueryHandlers_Must_Define_Authorization_Rules()
    {
        var applicationAssembly = typeof(IEventBus).Assembly;
        
        var handlerTypes = applicationAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && 
                        t.GetInterfaces().Any(i => i.IsGenericType && 
                        (i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) || 
                         i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>))))
            .ToList();

        Assert.NotEmpty(handlerTypes);

        var missingAuthorizationHandlers = new List<string>();

        foreach (var handler in handlerTypes)
        {
            // Decorators are skipped
            if (handler.Name.Contains("Decorator") || handler.Name.Contains("Behavior"))
                continue;

            var requestType = handler.GetInterfaces()
                .First(i => i.IsGenericType && 
                       (i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) || 
                        i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)))
                .GetGenericArguments()[0];

            bool hasHasPermission = requestType.GetCustomAttributes(typeof(Pos.Application.Common.Attributes.HasPermissionAttribute), true).Length > 0;
            bool hasAuthenticatedOnly = requestType.GetCustomAttributes(typeof(Pos.Application.Common.Attributes.AuthenticatedOnlyAttribute), true).Length > 0;
            bool hasPublicUseCase = requestType.GetCustomAttributes(typeof(Pos.Application.Common.Attributes.PublicUseCaseAttribute), true).Length > 0;

            if (!hasHasPermission && !hasAuthenticatedOnly && !hasPublicUseCase)
            {
                missingAuthorizationHandlers.Add(requestType.Name);
            }
        }

        Assert.True(
            missingAuthorizationHandlers.Count == 0,
            $"Los siguientes comandos o consultas NO definen reglas de autorización explícita (Fail-Closed). Agrega [HasPermission], [AuthenticatedOnly] o [PublicUseCase]: {string.Join(", ", missingAuthorizationHandlers)}"
        );
    }

    [Fact]
    public void All_TenantOwned_Entities_MustBeCoveredBy_RowLevelSecurityPolicy()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Pos.Infrastructure.Persistence.Context.PosDbContext>()
            .UseInMemoryDatabase("GovernanceRlsArchitectureTestDb")
            .Options;

        using var dbContext = new Pos.Infrastructure.Persistence.Context.PosDbContext(options, currentTenantId: Guid.NewGuid());
        var entityTypes = dbContext.Model.GetEntityTypes();

        var coveredTablesInSecurityPolicy = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Users", "Roles", "Products", "Categories", "Suppliers",
            "InventoryMovements", "Customers", "CashRegisters", "CashRegisterSessions",
            "Sales", "PurchaseOrders", "Warehouses", "Containers",
            "StockLevels", "StockAdjustments", "StockTransfers", "Payments",
            "Invoices", "AuditLogs", "Branches", "PosDevices",
            "SystemNotifications", "OutboxMessages", "AgentActionRecords"
        };

        var unhandledTables = new List<string>();

        foreach (var entityType in entityTypes)
        {
            if (entityType.IsOwned()) continue;

            var clrType = entityType.ClrType;
            if (clrType == null) continue;

            if (clrType.Name == "Tenant") continue; // Entidad de plataforma

            var hasTenantIdProperty = clrType.GetProperty("TenantId") != null;
            if (hasTenantIdProperty)
            {
                string tableName = entityType.GetTableName() ?? clrType.Name;
                if (!coveredTablesInSecurityPolicy.Contains(tableName))
                {
                    unhandledTables.Add($"{clrType.Name} (Tabla: {tableName})");
                }
            }
        }

        Assert.True(
            unhandledTables.Count == 0,
            $"Las siguientes entidades multi-tenant NO están cubiertas por la SECURITY POLICY de RLS en SQL Server: {string.Join(", ", unhandledTables)}"
        );
    }

    [Fact]
    public void AllowGlobalUserLookup_MustOnlyBeReferencedBy_AuthUserLookup()
    {
        var applicationAssembly = typeof(IEventBus).Assembly;
        var typesInApplication = applicationAssembly.GetTypes();

        foreach (var type in typesInApplication)
        {
            Assert.DoesNotContain("AllowGlobalUserLookup", type.Name);
        }

        var infrastructureAssembly = typeof(DependencyInjection).Assembly;
        var repositoryTypes = infrastructureAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == "Pos.Infrastructure.Persistence.Repositories")
            .ToList();

        Assert.NotEmpty(repositoryTypes);

        foreach (var repo in repositoryTypes)
        {
            var methods = repo.GetMethods();
            foreach (var method in methods)
            {
                Assert.DoesNotContain("AllowGlobalUserLookup", method.Name);
            }
        }
    }

    [Fact]
    public void Dispatcher_MustNot_Use_Dynamic_Or_Runtime_Binder()
    {
        var dispatcherType = typeof(Pos.Application.Common.Dispatching.Dispatcher);
        var referencedAssemblies = dispatcherType.Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(referencedAssemblies, a => a.Name?.Contains("Microsoft.CSharp") == true);
    }

    [Fact]
    public void All_Controllers_And_Actions_Must_Have_ApiVersion_And_Explicit_RateLimitingPolicy()
    {
        var apiAssembly = typeof(Program).Assembly;
        var controllerTypes = apiAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t))
            .ToList();

        Assert.NotEmpty(controllerTypes);

        var missingApiVersionControllers = new List<string>();
        var unhandledRateLimitingActions = new List<string>();

        foreach (var controller in controllerTypes)
        {
            // Check [ApiVersion]
            var hasApiVersion = controller.GetCustomAttributes(typeof(Asp.Versioning.ApiVersionAttribute), true).Length > 0;
            if (!hasApiVersion)
            {
                missingApiVersionControllers.Add(controller.Name);
            }

            var controllerHasEnable = controller.GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), true).Length > 0;
            var controllerHasDisable = controller.GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.DisableRateLimitingAttribute), true).Length > 0;

            var actions = controller.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute), true).Length > 0)
                .ToList();

            foreach (var action in actions)
            {
                var actionHasEnable = action.GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), true).Length > 0;
                var actionHasDisable = action.GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.DisableRateLimitingAttribute), true).Length > 0;

                bool isRateLimitingResolved = controllerHasEnable || controllerHasDisable || actionHasEnable || actionHasDisable;

                if (!isRateLimitingResolved)
                {
                    unhandledRateLimitingActions.Add($"{controller.Name}.{action.Name}");
                }
            }
        }

        Assert.True(
            missingApiVersionControllers.Count == 0,
            $"Los siguientes controladores no definen el atributo [ApiVersion]: {string.Join(", ", missingApiVersionControllers)}"
        );

        Assert.True(
            unhandledRateLimitingActions.Count == 0,
            $"Las siguientes acciones/controladores no definen una política de rate limiting explícita ([EnableRateLimiting] o [DisableRateLimiting]): {string.Join(", ", unhandledRateLimitingActions)}"
        );
    }

    [Fact]
    public void Controllers_Must_Use_ToActionResult_And_Not_Access_Result_Value_Directly()
    {
        var apiDir = SolutionDirectory.PosApi;
        var controllersDir = Path.Combine(apiDir, "Controllers");
        var resultExtensionsFile = Path.Combine(apiDir, "Extensions", "ResultExtensions.cs");

        Assert.True(Directory.Exists(controllersDir), $"El directorio de controladores '{controllersDir}' no existe.");
        Assert.True(File.Exists(resultExtensionsFile), $"El archivo de extensiones '{resultExtensionsFile}' no existe.");

        // Confirm ResultExtensions.cs IS the authorized place accessing .Value
        var resultExtensionsContent = File.ReadAllText(resultExtensionsFile);
        Assert.Contains(".Value", resultExtensionsContent);

        var controllerFiles = Directory.GetFiles(controllersDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(controllerFiles);

        var resultValueRegex = new System.Text.RegularExpressions.Regex(@"\b(result|res|commandResult|queryResult)\.Value\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var violations = new List<string>();

        foreach (var file in controllerFiles)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();
                if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                    trimmed.StartsWith("/*", StringComparison.Ordinal) ||
                    trimmed.StartsWith('*'))
                    continue;

                if (resultValueRegex.IsMatch(line))
                {
                    var relativePath = Path.GetRelativePath(SolutionDirectory.Root, file);
                    violations.Add($"{relativePath} (Línea {i + 1}): {trimmed}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Se detectaron accesos directos prohibidos a '.Value' de Result en controladores. Se debe usar 'this.ToActionResult(result)':\n{string.Join("\n", violations)}"
        );
    }

    [Fact]
    public void ClearDomainEvents_MustOnlyBeCalledFrom_InsertOutboxMessagesInterceptor()
    {
        var srcDir = Path.Combine(SolutionDirectory.Root, "src");
        Assert.True(Directory.Exists(srcDir), $"El directorio de código fuente '{srcDir}' no existe.");

        var csFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(csFiles);

        var clearEventsRegex = new System.Text.RegularExpressions.Regex(@"\.ClearDomainEvents\(\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var violations = new List<string>();

        foreach (var file in csFiles)
        {
            var fileName = Path.GetFileName(file);
            
            // Permitido en la definición del método base (AggregateRoot.cs) y en el interceptor
            if (fileName == "AggregateRoot.cs" || fileName == "InsertOutboxMessagesInterceptor.cs")
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();
                if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                    trimmed.StartsWith("/*", StringComparison.Ordinal) ||
                    trimmed.StartsWith('*'))
                    continue;

                if (clearEventsRegex.IsMatch(line))
                {
                    var relativePath = Path.GetRelativePath(SolutionDirectory.Root, file);
                    violations.Add($"{relativePath} (Línea {i + 1}): {trimmed}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Se detectaron llamadas prohibidas a 'ClearDomainEvents()'. Solo el InsertOutboxMessagesInterceptor tiene permiso para limpiar los eventos de dominio:\n{string.Join("\n", violations)}"
        );
    }

    [Fact]
    public void SetSuperAdmin_MustOnlyBeCalledFrom_OutboxProcessorBackgroundService()
    {
        var srcDir = Path.Combine(SolutionDirectory.Root, "src");
        Assert.True(Directory.Exists(srcDir), $"El directorio de código fuente '{srcDir}' no existe.");

        var csFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(csFiles);

        var setSuperAdminRegex = new System.Text.RegularExpressions.Regex(@"\.SetSuperAdmin\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var violations = new List<string>();

        foreach (var file in csFiles)
        {
            var fileName = Path.GetFileName(file);

            // Permitido únicamente en ITenantSetter.cs, CurrentTenantContext.cs y OutboxProcessorBackgroundService.cs
            if (fileName == "ITenantSetter.cs" ||
                fileName == "CurrentTenantContext.cs" ||
                fileName == "OutboxProcessorBackgroundService.cs")
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();
                if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                    trimmed.StartsWith("/*", StringComparison.Ordinal) ||
                    trimmed.StartsWith('*'))
                    continue;

                if (setSuperAdminRegex.IsMatch(line))
                {
                    var relativePath = Path.GetRelativePath(SolutionDirectory.Root, file);
                    violations.Add($"{relativePath} (Línea {i + 1}): {trimmed}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Se detectaron llamadas no autorizadas a 'SetSuperAdmin()'. Solo el OutboxProcessorBackgroundService tiene permiso para establecer el contexto de SuperAdmin en código de producción:\n{string.Join("\n", violations)}"
        );
    }

    [Fact]
    public void ResourceCreationEndpoints_Must_Use_ToCreatedResult_And_Declare_201Created()
    {
        var apiDir = SolutionDirectory.PosApi;
        var controllersDir = Path.Combine(apiDir, "Controllers");
        Assert.True(Directory.Exists(controllersDir), $"El directorio de controladores '{controllersDir}' no existe.");

        var creationControllerFiles = Directory.GetFiles(controllersDir, "*Controller.cs", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith("AuthController.cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(creationControllerFiles);

        var violations = new List<string>();

        // Acciones HTTP POST de creación de recursos direccionables
        var creationMethodNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CreateCategory",
            "CreateProduct",
            "CreateCustomer",
            "CreateSupplier",
            "CreateBranch",
            "CreateWarehouse",
            "CreateCashRegister",
            "RegisterPosDevice",
            "CreateNotification",
            "ProcessPayment",
            "IssueInvoice",
            "CreateSale",
            "CreatePurchaseOrder",
            "CreateTransfer",
            "CreateAdjustment",
            "CreateRole",
            "CreateUser",
            "CreateTenant",
            "ProposeAction",
            "RecordMovement"
        };

        foreach (var file in creationControllerFiles)
        {
            var content = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(SolutionDirectory.Root, file);

            foreach (var methodName in creationMethodNames)
            {
                if (!content.Contains($" {methodName}("))
                    continue;

                // Extraer el cuerpo del método o verificar que contenga ToCreatedResult
                if (!content.Contains("ToCreatedResult"))
                {
                    violations.Add($"{relativePath}: El método '{methodName}' debe retornar usando 'this.ToCreatedResult'.");
                }

                if (!content.Contains("StatusCodes.Status201Created"))
                {
                    violations.Add($"{relativePath}: El método '{methodName}' debe declarar [ProducesResponseType(..., StatusCodes.Status201Created)].");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Se detectaron endpoints de creación que no cumplen con el contrato HTTP 201 Created + ToCreatedResult:\n{string.Join("\n", violations)}"
        );
    }

    [Fact]
    public void FreeFirst_Policy_And_Production_Providers_Must_Comply_With_ADR0013()
    {
        var rootDir = SolutionDirectory.Root;
        var directoryPackagesFile = Path.Combine(rootDir, "Directory.Packages.props");
        Assert.True(File.Exists(directoryPackagesFile), $"No se encontró '{directoryPackagesFile}'.");

        var packagesDoc = System.Xml.Linq.XDocument.Load(directoryPackagesFile);
        var packageVersions = packagesDoc.Descendants("PackageVersion")
            .Select(x => new
            {
                Name = x.Attribute("Include")?.Value ?? string.Empty,
                Version = x.Attribute("Version")?.Value ?? string.Empty
            })
            .Where(x => !string.IsNullOrEmpty(x.Name))
            .ToDictionary(x => x.Name, x => x.Version, StringComparer.OrdinalIgnoreCase);

        // a) FluentAssertions debe ser < 7.0.0 (fijado en 6.12.2 por licencia comercial en v7+)
        if (packageVersions.TryGetValue("FluentAssertions", out var faVersion))
        {
            if (System.Version.TryParse(faVersion.Split('-')[0], out var semVer))
            {
                Assert.True(
                    semVer.Major < 7,
                    $"FluentAssertions debe ser < 7.0.0 para mantener licencia Apache 2.0 gratuita (ADR 0013). Versión detectada: {faVersion}"
                );
            }
        }

        // b) Proyectos en src/ NO deben referenciar librerías con licenciamiento comercial restringido
        var restrictedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MediatR",
            "AutoMapper",
            "MassTransit",
            "Duende.IdentityServer"
        };

        // c) Proyectos en src/ NO deben referenciar Sqlite ni proveedores no autorizados para producción (Hallazgo H11)
        var forbiddenSrcProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft.EntityFrameworkCore.Sqlite",
            "Microsoft.EntityFrameworkCore.InMemory"
        };

        // d) Pos.Domain y Directory.Build.props deben tener 0 PackageReference
        var domainCsproj = Path.Combine(rootDir, "src", "Pos.Domain", "Pos.Domain.csproj");
        Assert.True(File.Exists(domainCsproj), $"No se encontró '{domainCsproj}'.");
        var domainDoc = System.Xml.Linq.XDocument.Load(domainCsproj);
        var domainPackageRefs = domainDoc.Descendants("PackageReference").ToList();
        Assert.True(domainPackageRefs.Count == 0, $"Pos.Domain.csproj no debe contener PackageReference (debe tener 0). Encontrados: {domainPackageRefs.Count}.");

        var buildProps = Path.Combine(rootDir, "Directory.Build.props");
        Assert.True(File.Exists(buildProps), $"No se encontró '{buildProps}'.");
        var buildPropsDoc = System.Xml.Linq.XDocument.Load(buildProps);
        var buildPropsPackageRefs = buildPropsDoc.Descendants("PackageReference").ToList();
        Assert.True(buildPropsPackageRefs.Count == 0, $"Directory.Build.props no debe contener PackageReference (debe tener 0). Encontrados: {buildPropsPackageRefs.Count}.");

        var srcDir = Path.Combine(rootDir, "src");
        var csprojFiles = Directory.GetFiles(srcDir, "*.csproj", SearchOption.AllDirectories);
        Assert.NotEmpty(csprojFiles);

        var violations = new List<string>();

        foreach (var csproj in csprojFiles)
        {
            var projectDoc = System.Xml.Linq.XDocument.Load(csproj);
            var packageRefs = projectDoc.Descendants("PackageReference")
                .Select(x => x.Attribute("Include")?.Value ?? string.Empty)
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();

            var relativePath = Path.GetRelativePath(rootDir, csproj);

            foreach (var pkg in packageRefs)
            {
                if (restrictedPackages.Contains(pkg))
                {
                    violations.Add($"{relativePath}: Referencia prohibida a librería con licenciamiento comercial restringido '{pkg}'.");
                }

                if (forbiddenSrcProviders.Contains(pkg))
                {
                    violations.Add($"{relativePath}: Referencia prohibida a proveedor de base de datos no autorizado para producción '{pkg}' (H11).");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Violaciones de política Free-First y proveedores de producción detectadas:\n{string.Join("\n", violations)}"
        );
    }

    [Fact]
    public void Options_In_Api_Must_Use_ValidateOnStart_And_Not_Direct_Configure()
    {
        var rootDir = SolutionDirectory.Root;
        var apiDir = Path.Combine(rootDir, "src", "Pos.Api");
        var csFiles = Directory.GetFiles(apiDir, "*.cs", SearchOption.AllDirectories);

        var violations = new List<string>();

        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(rootDir, file);

            // Detectar llamadas a .Configure<T>( sin ValidateOnStart
            var matches = System.Text.RegularExpressions.Regex.Matches(content, @"\.Configure\s*<([^>]+)>\s*\(");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                violations.Add($"{relativePath}: Uso no autorizado de direct '.Configure<{match.Groups[1].Value}>()'. Debe usarse '.AddOptions<{match.Groups[1].Value}>().Bind(...).ValidateDataAnnotations().ValidateOnStart()'.");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Se encontraron usos directos de Configure<T>() en Pos.Api sin ValidateOnStart:\n{string.Join("\n", violations)}"
        );
    }

    [Fact]
    public void All_Layers_Must_Use_TimeProvider_With_Domain_Ratchet_Guard()
    {
        var rootDir = SolutionDirectory.Root;
        var srcDir = Path.Combine(rootDir, "src");
        var csFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories);

        var violations = new List<string>();

        foreach (var file in csFiles)
        {
            var relativePath = Path.GetRelativePath(rootDir, file).Replace('\\', '/');
            var fileName = Path.GetFileName(file);
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(File.ReadAllText(file));
            var root = tree.GetRoot();

            var findings = SystemClockRatchetScanner.Scan(root, relativePath);

            foreach (var finding in findings)
            {
                // En Pos.Domain, se permite únicamente la lista ratchet explícita de 23 entidades
                if (relativePath.StartsWith("src/Pos.Domain/", StringComparison.OrdinalIgnoreCase))
                {
                    if (!SystemClockRatchetScanner.AllowedDomainEntitiesRatchet.Contains(fileName))
                    {
                        violations.Add($"{finding.FilePath}:{finding.LineNumber} -> {finding.MemberAccess} ({finding.LineSnippet}) no está permitido en Pos.Domain fuera de la lista ratchet.");
                    }
                }
                else
                {
                    // En Pos.Api, Pos.Application y Pos.Infrastructure: TOLERANCIA CERO (0)
                    violations.Add($"{finding.FilePath}:{finding.LineNumber} -> {finding.MemberAccess} ({finding.LineSnippet}) prohibido en capas externas. Debe inyectarse TimeProvider.");
                }
            }
        }

        Assert.Empty(SystemClockRatchetScanner.AllowedDomainEntitiesRatchet);

        Assert.True(
            violations.Count == 0,
            $"Violaciones de reloj del sistema detectadas (debe usarse TimeProvider):\n{string.Join("\n", violations)}"
        );
    }

    [Fact]
    public void Adr_Files_Must_Live_Exclusively_In_Canonical_Directory()
    {
        var rootDir = SolutionDirectory.Root;
        var obsoleteAdrDir = Path.Combine(rootDir, "docs", "ADR");
        var canonicalAdrDir = Path.Combine(rootDir, "docs", "architecture", "adr");

        Assert.False(Directory.Exists(obsoleteAdrDir), "La carpeta obsoleta 'docs/ADR' no debe existir; todos los ADRs deben residir en 'docs/architecture/adr/'.");
        Assert.True(Directory.Exists(canonicalAdrDir), "La carpeta canónica 'docs/architecture/adr/' debe existir.");

        var adrFiles = Directory.GetFiles(canonicalAdrDir, "*.md")
            .Select(Path.GetFileName)
            .Where(f => f is not null)
            .Select(f => f!)
            .ToList();

        for (int i = 1; i <= 18; i++)
        {
            var prefix = $"{i:D4}-";
            bool exists = adrFiles.Any(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            Assert.True(exists, $"Falta el ADR requerido '{prefix}*.md' en '{canonicalAdrDir}'.");
        }
    }

    [Fact]
    public void SaveChanges_Interceptors_Must_Be_Registered_Only_Via_Canonical_Extension_And_Seeders_Must_Have_Zero_Reflection()
    {
        var rootDir = SolutionDirectory.Root;
        var srcDir = Path.Combine(rootDir, "src");
        var seedDir = Path.Combine(srcDir, "Pos.Infrastructure", "Persistence", "Seed");

        // 1. src/Pos.Infrastructure/Persistence/Seed/ debe tener 0 usos de System.Reflection / BindingFlags
        if (Directory.Exists(seedDir))
        {
            var seedFiles = Directory.GetFiles(seedDir, "*.cs", SearchOption.AllDirectories);
            var seedViolations = new List<string>();

            foreach (var file in seedFiles)
            {
                var text = File.ReadAllText(file);
                if (text.Contains("BindingFlags") || text.Contains("System.Reflection") || text.Contains("GetConstructor(") || text.Contains("GetProperty("))
                {
                    seedViolations.Add($"Uso indebido de reflexión en seeder: {Path.GetFileName(file)}");
                }
            }

            Assert.True(seedViolations.Count == 0,
                $"Se detectó uso indebido de reflexión en seeders:\n{string.Join("\n", seedViolations)}");
        }

        // 2. Fuera de PosDbContextInterceptorExtensions.cs ningún archivo debe registrar manualmente la tripleta de interceptores
        var allSourceFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(rootDir, "tests"), "*.cs", SearchOption.AllDirectories))
            .Where(f => !Path.GetFileName(f).Equals("PosDbContextInterceptorExtensions.cs", StringComparison.OrdinalIgnoreCase))
            .Where(f => !Path.GetFileName(f).Equals("ArchitectureTests.cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var registrationViolations = new List<string>();
        foreach (var file in allSourceFiles)
        {
            var text = File.ReadAllText(file);
            if (text.Contains("AddInterceptors") &&
                text.Contains("TenantSaveChangesInterceptor") &&
                text.Contains("AuditSaveChangesInterceptor") &&
                text.Contains("InsertOutboxMessagesInterceptor"))
            {
                if (Path.GetFileName(file).Equals("PosDbContext.cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                registrationViolations.Add($"Registro manual de la tripleta de interceptores en: {Path.GetFileName(file)}");
            }
        }

        Assert.True(registrationViolations.Count == 0,
            $"Fuera de PosDbContextInterceptorExtensions.cs ningún archivo debe registrar manualmente la tripleta de interceptores de SaveChanges:\n{string.Join("\n", registrationViolations)}");
    }

    [Fact]
    public void Domain_And_Application_Must_Not_Have_Phase_Named_Files_Or_Mixed_Decorator_Names_In_Behaviors()
    {
        var rootDir = SolutionDirectory.Root;
        var srcDir = Path.Combine(rootDir, "src");

        // a) Existe algún archivo cuyo nombre empiece por Phase en src/
        var phaseFiles = Directory.GetFiles(srcDir, "Phase*.*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(rootDir, f))
            .ToList();

        Assert.True(
            phaseFiles.Count == 0,
            $"Se encontraron archivos temporales nombrados con prefijo 'Phase' en src/:\n{string.Join("\n", phaseFiles)}"
        );

        // b) Existe alguna clase en src/Pos.Application/Common/Behaviors/ cuyo nombre termine en Decorator en vez de Behavior
        var behaviorsDir = Path.Combine(srcDir, "Pos.Application", "Common", "Behaviors");
        if (Directory.Exists(behaviorsDir))
        {
            var behaviorFiles = Directory.GetFiles(behaviorsDir, "*.cs");
            var decoratorViolations = new List<string>();

            foreach (var file in behaviorFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.EndsWith("Decorator", StringComparison.OrdinalIgnoreCase))
                {
                    decoratorViolations.Add($"El archivo '{Path.GetFileName(file)}' termina en 'Decorator' en vez de 'Behavior'.");
                }

                var text = File.ReadAllText(file);
                var match = System.Text.RegularExpressions.Regex.Match(text, @"public\s+class\s+(\w+Decorator)\b");
                if (match.Success)
                {
                    decoratorViolations.Add($"La clase '{match.Groups[1].Value}' en '{Path.GetFileName(file)}' termina en 'Decorator' en vez de 'Behavior'.");
                }
            }

            Assert.True(
                decoratorViolations.Count == 0,
                $"Se detectó nomenclatura no homologada 'Decorator' en Common/Behaviors:\n{string.Join("\n", decoratorViolations)}"
            );
        }
    }

    [Fact]
    public void Src_Must_Not_Contain_Any_GitKeep_Files()
    {
        var srcDir = Path.Combine(SolutionDirectory.Root, "src");
        var gitkeepFiles = Directory.GetFiles(srcDir, ".gitkeep", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(SolutionDirectory.Root, f))
            .ToList();

        Assert.True(
            gitkeepFiles.Count == 0,
            $"Se encontraron archivos '.gitkeep' en 'src/':\n{string.Join("\n", gitkeepFiles)}"
        );
    }

    [Fact]
    public void Repository_Interfaces_Must_Reside_Only_In_PosDomain_Interfaces()
    {
        var rootDir = SolutionDirectory.Root;
        var srcDir = Path.Combine(rootDir, "src");
        var canonicalInterfacesDir = Path.GetFullPath(Path.Combine(srcDir, "Pos.Domain", "Interfaces"));

        var allCsFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories);
        var violations = new List<string>();

        foreach (var file in allCsFiles)
        {
            var fileDir = Path.GetDirectoryName(file);
            var isCanonicalDir = string.Equals(fileDir, canonicalInterfacesDir, StringComparison.OrdinalIgnoreCase);

            var lines = File.ReadAllLines(file);
            foreach (var line in lines)
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"public\s+interface\s+(I\w+Repository)\b");
                if (match.Success)
                {
                    var interfaceName = match.Groups[1].Value;
                    if (!isCanonicalDir)
                    {
                        violations.Add($"Interfaz de repositorio '{interfaceName}' declarada fuera de 'Pos.Domain/Interfaces/' en: '{Path.GetRelativePath(rootDir, file)}'");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Se encontraron interfaces de repositorio fuera de Pos.Domain/Interfaces:\n{string.Join("\n", violations)}"
        );
    }

    [Fact]
    public void DomainEvents_Files_Must_Follow_Events_Convention()
    {
        var rootDir = SolutionDirectory.Root;
        var domainEventsDir = Path.Combine(rootDir, "src", "Pos.Domain", "DomainEvents");

        Assert.True(Directory.Exists(domainEventsDir), $"No existe el directorio '{domainEventsDir}'.");

        var csFiles = Directory.GetFiles(domainEventsDir, "*.cs")
            .Select(Path.GetFileName)
            .ToList();

        var nonConformingFiles = csFiles
            .Where(f => !f!.EndsWith("Events.cs", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            nonConformingFiles.Count == 0,
            $"Se encontraron archivos en DomainEvents que no siguen la convención '*Events.cs':\n{string.Join("\n", nonConformingFiles)}"
        );
    }

    [Fact]
    public void ADR_Files_Must_Be_Strictly_Lowercase_Kebab_Case()
    {
        var rootDir = SolutionDirectory.Root;
        var adrDir = Path.Combine(rootDir, "docs", "architecture", "adr");

        Assert.True(Directory.Exists(adrDir), $"No existe el directorio '{adrDir}'.");

        var mdFiles = Directory.GetFiles(adrDir, "*.md")
            .Select(Path.GetFileName)
            .ToList();

        var violations = new List<string>();
        var kebabRegex = new System.Text.RegularExpressions.Regex(@"^[a-z0-9]+(-[a-z0-9]+)*\.md$");

        foreach (var file in mdFiles)
        {
            if (file != null && !kebabRegex.IsMatch(file))
            {
                violations.Add($"El archivo ADR '{file}' no sigue la convención estricta en minúsculas kebab-case.");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Se detectaron archivos ADR con nombres inválidos (deben ser estrictamente minúsculas kebab-case):\n{string.Join("\n", violations)}"
        );
    }
}

