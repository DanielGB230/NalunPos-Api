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
}
