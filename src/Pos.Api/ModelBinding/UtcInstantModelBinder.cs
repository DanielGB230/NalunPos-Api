using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Pos.Application.Common.Validation;

namespace Pos.Api.ModelBinding;

/// <summary>
/// Binder de borde para <see cref="DateTime"/>/<see cref="DateTime?"/> en query string.
/// Materializa el contrato del filtro de rango de fechas: solo instantes UTC con offset
/// explícito. Un valor ambiguo lanza <see cref="ValidationException"/> y el manejador global
/// lo traduce a <c>400 ProblemDetails</c> con el código estable
/// <see cref="DateRangeRules.OffsetRequiredCode"/>.
/// </summary>
public sealed class UtcInstantModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        ValueProviderResult valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None
            || string.IsNullOrWhiteSpace(valueProviderResult.FirstValue))
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        if (!UtcInstantQueryParser.TryParse(valueProviderResult.FirstValue, out DateTime utcInstant))
        {
            throw new ValidationException(
            [
                new ValidationFailure(bindingContext.ModelName, DateRangeRules.OffsetRequiredMessage)
                {
                    ErrorCode = DateRangeRules.OffsetRequiredCode
                }
            ]);
        }

        bindingContext.Result = ModelBindingResult.Success(utcInstant);
        return Task.CompletedTask;
    }
}
