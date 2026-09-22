using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
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
}

