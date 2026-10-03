using FluentValidation.Results;
using Pos.Domain.Common;

namespace Pos.Application.Common.Validation;

/// <summary>
/// Fábrica estática para transformar errores de FluentValidation (<see cref="ValidationFailure"/>)
/// en objetos <see cref="DomainError"/> y tipos de retorno <see cref="Result"/> / <see cref="Result{T}"/>.
/// Utiliza delegados creados dinámicamente y cacheados por tipo de respuesta para evitar el overhead de reflexión en cada llamada.
/// </summary>
public static class ValidationResultFactory
{
    private const string ValidationErrorCode = "Validation.Error";
    private const string SummaryMessage = "Uno o más errores de validación ocurrieron.";

    /// <summary>
    /// Construye un <see cref="DomainError"/> de tipo Validación agrupando los fallos por nombre de propiedad.
    /// Preserva la comparación ordinal para los nombres de propiedad y el orden original de los mensajes de error.
    /// </summary>
    public static DomainError CreateError(IEnumerable<ValidationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        var errorDict = failures
            .Where(f => f != null)
            .GroupBy(f => f.PropertyName ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray(),
                StringComparer.Ordinal
            );

        var fieldErrors = new FieldErrors(errorDict);
        return DomainError.Validation(ValidationErrorCode, SummaryMessage, fieldErrors);
    }

    /// <summary>
    /// Indica si el tipo <typeparamref name="TResponse"/> representa un <see cref="Result"/> o <see cref="Result{T}"/>.
    /// </summary>
    public static bool IsResultResponse<TResponse>()
    {
        var type = typeof(TResponse);
        return type == typeof(Result) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Result<>));
    }

    /// <summary>
    /// Crea una instancia del tipo de respuesta <typeparamref name="TResponse"/> (que debe ser <see cref="Result"/> o <see cref="Result{T}"/>)
    /// utilizando un delegado cacheado por tipo de respuesta.
    /// </summary>
    /// <exception cref="InvalidOperationException">Si <typeparamref name="TResponse"/> no es <see cref="Result"/> ni <see cref="Result{T}"/>.</exception>
    public static TResponse CreateResultForResponse<TResponse>(IEnumerable<ValidationFailure> failures)
    {
        if (!IsResultResponse<TResponse>())
        {
            throw new InvalidOperationException($"El tipo de respuesta '{typeof(TResponse).Name}' no es un Result ni Result<T>.");
        }

        var error = CreateError(failures);
        return ResponseFailCache<TResponse>.Value(error);
    }

    private static class ResponseFailCache<TResponse>
    {
        public static readonly Func<DomainError, TResponse> Value = CreateDelegate();

        private static Func<DomainError, TResponse> CreateDelegate()
        {
            var responseType = typeof(TResponse);

            if (responseType == typeof(Result))
            {
                return error => (TResponse)(object)Result.Failure(error);
            }

            if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                var valueType = responseType.GetGenericArguments()[0];
                var method = typeof(Result)
                    .GetMethod(nameof(Result.Fail), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, [typeof(DomainError)])!
                    .MakeGenericMethod(valueType);

                return (Func<DomainError, TResponse>)Delegate.CreateDelegate(typeof(Func<DomainError, TResponse>), method);
            }

            throw new InvalidOperationException($"El tipo de respuesta '{responseType.Name}' no es un Result ni Result<T>.");
        }
    }
}
