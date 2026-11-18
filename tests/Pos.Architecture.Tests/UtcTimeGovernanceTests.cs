using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Pos.Application.Common.Interfaces;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Context;
using Xunit;

namespace Pos.Architecture.Tests;

/// <summary>
/// Guardas del contrato de tiempo (ADR 0026):
///   1. Toda propiedad <c>DateTime</c>/<c>DateTime?</c> de entidades de dominio y DTOs/Responses
///      debe llamarse <c>*Utc</c> salvo exclusión explícita y documentada.
///   2. En el modelo real de EF Core, toda propiedad <c>*Utc</c> debe tener el conversor UTC aplicado.
/// </summary>
public class UtcTimeGovernanceTests
{
    /// <summary>
    /// Exclusión explícita: <c>InventoryMovement.ExpirationDate</c> es una fecha de calendario
    /// (vencimiento de lote), no un instante. No debe interpretarse en una zona horaria.
    /// Los filtros de calendario de los Requests (<c>StartDate</c>/<c>EndDate</c>) también quedan
    /// fuera por la misma razón y no forman parte de esta guarda (solo Domain + DTOs/Responses).
    /// </summary>
    private static readonly HashSet<string> AllowedCalendarDateProperties =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "Pos.Domain.Entities.InventoryMovement.ExpirationDate"
        };

    [Fact]
    public void DateTimeProperties_InDomainAndDtos_MustBeNamedWithUtcSuffix()
    {
        var typesToAudit = new List<Type>();

        Assembly domainAssembly = typeof(Pos.Domain.Common.Entity<>).Assembly;
        typesToAudit.AddRange(domainAssembly.GetTypes()
            .Where(t => t.IsClass && t.Namespace == "Pos.Domain.Entities"));

        Assembly applicationAssembly = typeof(IEventBus).Assembly;
        typesToAudit.AddRange(applicationAssembly.GetTypes()
            .Where(t => t.Namespace != null &&
                        t.Namespace.Contains(".DTOs", StringComparison.Ordinal)));

        Assembly apiAssembly = typeof(Program).Assembly;
        typesToAudit.AddRange(apiAssembly.GetTypes()
            .Where(t => t.Namespace == "Pos.Api.Contracts.Responses"));

        Assert.NotEmpty(typesToAudit);

        var auditedProperties = 0;
        var violations = new List<string>();

        foreach (Type type in typesToAudit)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                Type propertyType = property.PropertyType;
                if (propertyType != typeof(DateTime) && propertyType != typeof(DateTime?))
                {
                    continue;
                }

                auditedProperties++;

                if (property.Name.EndsWith(UtcDateTimeConvention.UtcSuffix, StringComparison.Ordinal))
                {
                    continue;
                }

                string qualifiedName = $"{type.FullName}.{property.Name}";
                if (AllowedCalendarDateProperties.Contains(qualifiedName))
                {
                    continue;
                }

                violations.Add(
                    $"{qualifiedName} es DateTime/DateTime? pero no termina en 'Utc'. " +
                    "Si es un instante, renómbrala a *Utc; si es una fecha de calendario, agrégala a la lista de exclusión documentada.");
            }
        }

        Assert.True(auditedProperties > 0, "La guarda de nombres no auditó ninguna propiedad DateTime.");
        Assert.True(
            violations.Count == 0,
            $"Propiedades DateTime sin sufijo 'Utc' fuera de la lista de exclusión:\n{string.Join("\n", violations)}");
    }

    [Fact]
    public void AllowedCalendarDateProperties_MustReferenceExistingProperties()
    {
        var unresolved = new List<string>();

        foreach (string qualifiedName in AllowedCalendarDateProperties)
        {
            int lastDot = qualifiedName.LastIndexOf('.');
            string typeName = qualifiedName[..lastDot];
            string propertyName = qualifiedName[(lastDot + 1)..];

            Type? type = typeof(Pos.Domain.Common.Entity<>).Assembly.GetType(typeName);
            if (type == null || type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance) == null)
            {
                unresolved.Add(qualifiedName);
            }
        }

        Assert.True(
            unresolved.Count == 0,
            $"La lista de exclusión de fechas de calendario referencia propiedades inexistentes:\n{string.Join("\n", unresolved)}");
    }

    [Fact]
    public void EveryUtcProperty_InEfModel_HasUtcValueConverter()
    {
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseInMemoryDatabase("GovernanceUtcTimeArchitectureTestDb")
            .Options;

        using var dbContext = new PosDbContext(options);

        var utcProperties = dbContext.Model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetProperties())
            .Where(property => UtcDateTimeConvention.IsUtcInstantProperty(property.ClrType, property.Name))
            .ToList();

        Assert.NotEmpty(utcProperties);

        var violations = new List<string>();

        foreach (IProperty property in utcProperties)
        {
            var converter = property.GetValueConverter();
            if (converter == null)
            {
                violations.Add($"{property.DeclaringType.DisplayName()}.{property.Name} no tiene ValueConverter UTC.");
                continue;
            }

            Type expectedClrType = property.ClrType;
            if (converter.ModelClrType != expectedClrType)
            {
                violations.Add(
                    $"{property.DeclaringType.DisplayName()}.{property.Name} tiene un conversor con ModelClrType " +
                    $"'{converter.ModelClrType}' en lugar de '{expectedClrType}'.");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Propiedades *Utc sin el conversor del contrato de tiempo:\n{string.Join("\n", violations)}");
    }
}
