using System.Globalization;

namespace Pos.Api.ModelBinding;

/// <summary>
/// Parser único del contrato de filtros de rango de fechas en query string.
/// Acepta exclusivamente un instante completo (fecha + hora) con offset explícito
/// (<c>Z</c> o <c>±HH:MM</c>). Un valor sin offset es ambiguo y se rechaza en el borde:
/// nunca se interpreta con la zona horaria del servidor.
/// </summary>
public static class UtcInstantQueryParser
{
    /// <summary>
    /// Intenta convertir <paramref name="rawValue"/> en un <see cref="DateTime"/> con
    /// <c>Kind=Utc</c> que representa el instante absoluto indicado por el offset.
    /// </summary>
    public static bool TryParse(string? rawValue, out DateTime utcInstant)
    {
        utcInstant = default;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        string text = rawValue.Trim();

        // Una fecha sin hora (por ejemplo "2026-01-01") es una fecha de calendario, no un instante.
        int timeSeparatorIndex = text.IndexOf('T');
        if (timeSeparatorIndex < 0)
        {
            return false;
        }

        string timeSection = text[(timeSeparatorIndex + 1)..];
        if (!HasExplicitOffset(timeSection))
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset parsed))
        {
            return false;
        }

        utcInstant = parsed.UtcDateTime;
        return true;
    }

    private static bool HasExplicitOffset(string timeSection)
    {
        if (timeSection.EndsWith('Z') || timeSection.EndsWith('z'))
        {
            return true;
        }

        int length = timeSection.Length;

        // Formato ±HH:MM al final de la sección de hora.
        if (length >= 6
            && IsSign(timeSection[^6])
            && IsTwoDigits(timeSection[^5], timeSection[^4])
            && timeSection[^3] == ':'
            && IsTwoDigits(timeSection[^2], timeSection[^1]))
        {
            return true;
        }

        // Formato ±HHMM al final de la sección de hora.
        return length >= 5
            && IsSign(timeSection[^5])
            && IsTwoDigits(timeSection[^4], timeSection[^3])
            && IsTwoDigits(timeSection[^2], timeSection[^1]);
    }

    private static bool IsSign(char value) => value is '+' or '-';

    private static bool IsTwoDigits(char first, char second) =>
        char.IsAsciiDigit(first) && char.IsAsciiDigit(second);
}
