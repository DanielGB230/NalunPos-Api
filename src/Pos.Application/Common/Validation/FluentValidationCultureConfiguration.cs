using System.Globalization;
using FluentValidation;

namespace Pos.Application.Common.Validation;

/// <summary>
/// Configuración centralizada de la cultura predeterminada de FluentValidation.
/// Garantiza que los mensajes de error por defecto sean deterministas en español ("es") independientemente de la cultura del servidor.
/// </summary>
public static class FluentValidationCultureConfiguration
{
    public static void ConfigureDefaultCulture()
    {
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("es");
    }
}
