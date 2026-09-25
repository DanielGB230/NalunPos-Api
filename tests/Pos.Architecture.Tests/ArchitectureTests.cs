using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
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
}

