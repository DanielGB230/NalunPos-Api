using Microsoft.Extensions.Options;

namespace Pos.Api.Options;

/// <summary>
/// Adaptador de las reglas puras <see cref="CorsOriginsRules"/> al mecanismo de validación
/// de opciones de .NET (<see cref="IValidateOptions{TOptions}"/>). Sin estado y con una única
/// responsabilidad: construir el mensaje de fallo con la clave de configuración y el origen
/// ofensor concreto.
/// </summary>
public sealed class CorsOriginsOptionsValidator : IValidateOptions<CorsSettings>
{
    /// <summary>
    /// Valida la lista de orígenes configurada. Devuelve <see cref="ValidateOptionsResult.Fail"/>
    /// con el mensaje de <see cref="CorsOriginsRules.BuildFailureMessage"/> (clave de
    /// configuración + origen ofensor) o <see cref="ValidateOptionsResult.Success"/> si la
    /// lista es válida.
    /// </summary>
    public ValidateOptionsResult Validate(string? name, CorsSettings options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string? failureMessage = CorsOriginsRules.BuildFailureMessage(options.AllowedOrigins);

        return failureMessage is null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failureMessage);
    }
}
