using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Pos.Infrastructure.Persistence;

/// <summary>
/// Convención canónica del contrato de tiempo en persistencia.
///
/// Un instante se representa siempre como <see cref="DateTime"/> con <c>Kind=Utc</c> y
/// se serializa con sufijo <c>'Z'</c>. SQL Server (<c>datetime2</c>) no almacena el
/// <c>Kind</c>, por lo que al leer devuelve <c>Unspecified</c> y un cliente JavaScript lo
/// interpretaría como hora local.
///
/// Reglas que aplica esta convención a toda propiedad <c>DateTime</c> o <c>DateTime?</c>
/// cuyo nombre termina en <c>Utc</c>:
///   1. Lectura: el valor recuperado se marca con <c>Kind=Utc</c>.
///   2. Escritura: fail-closed; si el valor no tiene <c>Kind=Utc</c> se lanza
///      <see cref="InvalidOperationException"/> en lugar de convertir en silencio.
///
/// Quedan fuera por diseño las fechas de calendario (por ejemplo
/// <c>InventoryMovement.ExpirationDate</c>): no son instantes y no deben interpretarse
/// en una zona horaria.
///
/// El tipo de columna no cambia: el conversor es <c>DateTime</c> → <c>DateTime</c>,
/// por lo que no genera migraciones.
/// </summary>
public static class UtcDateTimeConvention
{
    /// <summary>Sufijo obligatorio de toda propiedad que represente un instante.</summary>
    public const string UtcSuffix = "Utc";

    /// <summary>
    /// Aplica la convención a todas las propiedades del modelo. Debe invocarse en un
    /// único punto, después de <c>ApplyConfigurationsFromAssembly</c> y antes de finalizar
    /// la construcción del modelo.
    /// </summary>
    public static void Apply(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                if (!IsUtcInstantProperty(property.ClrType, property.Name))
                {
                    continue;
                }

                property.SetValueConverter(
                    property.ClrType == typeof(DateTime?)
                        ? CreateNullableConverter(property.Name)
                        : CreateConverter(property.Name));
            }
        }
    }

    /// <summary>
    /// Indica si la propiedad representa un instante UTC: tipo <c>DateTime</c> o
    /// <c>DateTime?</c> y nombre con sufijo <c>Utc</c>.
    /// </summary>
    public static bool IsUtcInstantProperty(Type clrType, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        bool isDateTime = clrType == typeof(DateTime) || clrType == typeof(DateTime?);
        return isDateTime && propertyName.EndsWith(UtcSuffix, StringComparison.Ordinal);
    }

    /// <summary>Conversor para propiedades <c>DateTime</c> no anulables.</summary>
    public static ValueConverter<DateTime, DateTime> CreateConverter(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return new ValueConverter<DateTime, DateTime>(
            value => ToDatabase(value, propertyName),
            value => FromDatabase(value));
    }

    /// <summary>Conversor para propiedades <c>DateTime?</c>.</summary>
    public static ValueConverter<DateTime?, DateTime?> CreateNullableConverter(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return new ValueConverter<DateTime?, DateTime?>(
            value => value.HasValue ? ToDatabase(value.Value, propertyName) : null,
            value => value.HasValue ? FromDatabase(value.Value) : null);
    }

    private static DateTime ToDatabase(DateTime value, string propertyName)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        throw new InvalidOperationException(
            $"Contrato de tiempo violado: la propiedad '{propertyName}' representa un instante UTC " +
            $"pero se intentó persistir un DateTime con Kind={value.Kind}. " +
            "Obtenga el instante con TimeProvider.GetUtcNow().UtcDateTime o conviértalo explícitamente a UTC.");
    }

    private static DateTime FromDatabase(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        throw new InvalidOperationException(
            $"Contrato de tiempo violado: se recuperó de la base de datos un DateTime con Kind={value.Kind}. " +
            "Un instante almacenado debe leerse en UTC, nunca en hora local.");
    }
}
