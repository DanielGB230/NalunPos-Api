using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Pos.Api.ModelBinding;

/// <summary>
/// Registra <see cref="UtcInstantModelBinder"/> para todo parámetro de query string
/// <see cref="DateTime"/> o <see cref="DateTime?"/>. Los cuerpos JSON no pasan por model
/// binders, por lo que las fechas de calendario (por ejemplo <c>ExpirationDate</c>) no se ven afectadas.
/// </summary>
public sealed class UtcInstantModelBinderProvider : IModelBinderProvider
{
    private static readonly UtcInstantModelBinder Binder = new();

    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Type modelType = context.Metadata.ModelType;
        return modelType == typeof(DateTime) || modelType == typeof(DateTime?)
            ? Binder
            : null;
    }
}
