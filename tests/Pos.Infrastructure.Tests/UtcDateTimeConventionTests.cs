using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pos.Infrastructure.Persistence;
using Xunit;

namespace Pos.Infrastructure.Tests;

/// <summary>
/// Contrato de tiempo a nivel de conversión (ADR 0026):
///   - Lectura: un <c>DateTime</c> sin <c>Kind</c> recuperado de la base se marca como UTC.
///   - Escritura: fail-closed, cualquier valor que no sea <c>Kind=Utc</c> lanza excepción.
/// </summary>
public class UtcDateTimeConventionTests
{
    [Fact]
    public void IsUtcInstantProperty_OnlyMatchesDateTimePropertiesWithUtcSuffix()
    {
        Assert.True(UtcDateTimeConvention.IsUtcInstantProperty(typeof(DateTime), "CreatedAtUtc"));
        Assert.True(UtcDateTimeConvention.IsUtcInstantProperty(typeof(DateTime?), "ClosedAtUtc"));

        Assert.False(UtcDateTimeConvention.IsUtcInstantProperty(typeof(DateTime), "ExpirationDate"));
        Assert.False(UtcDateTimeConvention.IsUtcInstantProperty(typeof(DateTime?), "ExpirationDate"));
        Assert.False(UtcDateTimeConvention.IsUtcInstantProperty(typeof(DateTimeOffset?), "LastPingUtc"));
        Assert.False(UtcDateTimeConvention.IsUtcInstantProperty(typeof(string), "NameUtc"));
    }

    [Fact]
    public void Converter_RoundTripsUtcValueWithoutAlteringIt()
    {
        ValueConverter<DateTime, DateTime> converter = UtcDateTimeConvention.CreateConverter("CreatedAtUtc");
        var value = new DateTime(2026, 11, 17, 10, 30, 0, DateTimeKind.Utc);

        var stored = (DateTime)converter.ConvertToProvider(value)!;
        var read = (DateTime)converter.ConvertFromProvider(stored)!;

        Assert.Equal(DateTimeKind.Utc, stored.Kind);
        Assert.Equal(value, read);
        Assert.Equal(DateTimeKind.Utc, read.Kind);
    }

    [Fact]
    public void Converter_WriteWithUnspecifiedKind_FailsClosed()
    {
        ValueConverter<DateTime, DateTime> converter = UtcDateTimeConvention.CreateConverter("CreatedAtUtc");
        var value = new DateTime(2026, 11, 17, 10, 30, 0, DateTimeKind.Unspecified);

        var exception = Assert.Throws<InvalidOperationException>(() => converter.ConvertToProvider(value));

        Assert.Contains("CreatedAtUtc", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Unspecified", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Converter_WriteWithLocalKind_FailsClosed()
    {
        ValueConverter<DateTime, DateTime> converter = UtcDateTimeConvention.CreateConverter("OccurredAtUtc");
        var value = new DateTime(2026, 11, 17, 10, 30, 0, DateTimeKind.Local);

        var exception = Assert.Throws<InvalidOperationException>(() => converter.ConvertToProvider(value));

        Assert.Contains("Local", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Converter_ReadUnspecifiedKind_MarksItAsUtc()
    {
        ValueConverter<DateTime, DateTime> converter = UtcDateTimeConvention.CreateConverter("CreatedAtUtc");
        var stored = new DateTime(2026, 11, 17, 10, 30, 0, DateTimeKind.Unspecified);

        var read = (DateTime)converter.ConvertFromProvider(stored)!;

        Assert.Equal(DateTimeKind.Utc, read.Kind);
        Assert.Equal(stored.Ticks, read.Ticks);
    }

    [Fact]
    public void NullableConverter_PreservesNullAndMarksNonNullValuesAsUtc()
    {
        ValueConverter<DateTime?, DateTime?> converter = UtcDateTimeConvention.CreateNullableConverter("UpdatedAtUtc");

        Assert.Null(converter.ConvertToProvider(null));
        Assert.Null(converter.ConvertFromProvider(null));

        var value = new DateTime(2026, 11, 17, 10, 30, 0, DateTimeKind.Utc);
        Assert.Equal(DateTimeKind.Utc, ((DateTime)converter.ConvertToProvider(value)!).Kind);

        var stored = new DateTime(2026, 11, 17, 10, 30, 0, DateTimeKind.Unspecified);
        var read = (DateTime)converter.ConvertFromProvider(stored)!;
        Assert.Equal(DateTimeKind.Utc, read.Kind);
    }

    [Fact]
    public void NullableConverter_WriteWithNonUtcKind_FailsClosed()
    {
        ValueConverter<DateTime?, DateTime?> converter = UtcDateTimeConvention.CreateNullableConverter("UpdatedAtUtc");
        var value = new DateTime(2026, 11, 17, 10, 30, 0, DateTimeKind.Unspecified);

        Assert.Throws<InvalidOperationException>(() => converter.ConvertToProvider(value));
    }
}
