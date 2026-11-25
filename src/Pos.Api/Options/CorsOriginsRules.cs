using System;
using System.Collections.Generic;

namespace Pos.Api.Options;

/// <summary>
/// Reglas puras de validación de orígenes CORS (fail-fast en el arranque, ADR 0035).
/// Una sola responsabilidad: decidir si una lista de orígenes es válida y construir el
/// mensaje de error que incluye la clave de configuración y el origen ofensor.
/// No lee configuración, no registra servicios ni conoce el pipeline de CORS.
///
/// Reglas: URI absoluta; esquema http o https; host presente; sin path (ni el "/" final);
/// sin query, fragment ni userinfo; no "*" ni "null"; lista no vacía.
/// </summary>
public static class CorsOriginsRules
{
    /// <summary>Clave de configuración que aparece en todos los mensajes de error.</summary>
    public const string SectionKey = CorsSettings.SectionName + ":AllowedOrigins";

    /// <summary>
    /// Mensaje de fallo genérico del registro de opciones: identifica la clave de
    /// configuración y resume las reglas. El detalle del origen ofensor concreto lo
    /// añade <see cref="CorsOriginsOptionsValidator"/>.
    /// </summary>
    public const string ValidationFailureMessage =
        "CorsSettings:AllowedOrigins es inválida: la lista de orígenes CORS no puede estar vacía y cada " +
        "origen debe ser una URI absoluta con esquema http o https, host presente y sin path, query, " +
        "fragment ni userinfo (tampoco se admiten \"*\" ni \"null\").";

    /// <summary>
    /// Terminadores que sólo pueden aparecer después de la autoridad si el origen
    /// lleva path, query o fragment.
    /// </summary>
    private static readonly char[] s_afterAuthority = ['/', '?', '#'];

    /// <summary>
    /// Devuelve el primer origen inválido de la lista, o <c>null</c> si todos son válidos.
    /// Una lista vacía también devuelve <c>null</c>: ese caso lo gestiona
    /// <see cref="BuildFailureMessage"/>, porque no existe un origen ofensor.
    /// </summary>
    public static string? FindInvalidOrigin(IReadOnlyList<string>? origins)
    {
        if (origins is null)
        {
            return null;
        }

        for (int i = 0; i < origins.Count; i++)
        {
            if (!IsValidOrigin(origins[i]))
            {
                return origins[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Predicado de validación para el registro de opciones: <c>true</c> sólo si la lista
    /// de orígenes es válida (no vacía y sin ningún origen inválido).
    /// </summary>
    public static bool HasValidOrigins(IReadOnlyList<string>? origins)
    {
        if (origins is null || origins.Count == 0)
        {
            return false;
        }

        return FindInvalidOrigin(origins) is null;
    }

    /// <summary>Valida un origen individual según las reglas documentadas en la clase.</summary>
    public static bool IsValidOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        if (origin == "*" || string.Equals(origin, "null", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (string.IsNullOrEmpty(uri.Host))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        // Uri normaliza "http://h:4200" y "http://h:4200/" a la misma forma canónica
        // (AbsolutePath == "/"), así que el path — incluido el "/" final — se detecta
        // sobre la cadena original: nada puede sobrar tras la autoridad.
        int schemeSeparator = origin.IndexOf("://", StringComparison.Ordinal);
        int authorityStart = schemeSeparator + 3;
        if (origin.IndexOfAny(s_afterAuthority, authorityStart) >= 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Mensaje de error completo (clave de configuración y, si existe, origen ofensor),
    /// o <c>null</c> si la lista es válida.
    /// </summary>
    public static string? BuildFailureMessage(IReadOnlyList<string>? origins)
    {
        if (origins is null || origins.Count == 0)
        {
            return $"{SectionKey}: la lista de orígenes no puede estar vacía. Configure al menos un origen. " +
                   "Fail-closed: sin orígenes no se admite ninguna petición cross-origin.";
        }

        string? invalid = FindInvalidOrigin(origins);
        if (invalid is not null)
        {
            return $"{SectionKey}: origen inválido '{invalid}'. Debe ser URI absoluta con esquema http o https y host " +
                   "presente, sin path (ni \"/\"), query, fragment ni userinfo; tampoco se admite \"*\" ni \"null\".";
        }

        return null;
    }
}
