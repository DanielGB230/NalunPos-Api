using FluentValidation;
using FluentValidation.Results;

namespace Pos.Application.Common.Validation;

/// <summary>
/// Helper estático para ejecutar validadores de FluentValidation de forma secuencial.
/// Utilizado por <see cref="Behaviors.ValidationDecorator{TCommand, TResponse}"/> y
/// <see cref="Behaviors.ValidationQueryDecorator{TQuery, TResponse}"/>.
/// </summary>
public static class ValidationRunner
{
    public static async Task<List<ValidationFailure>> ValidateAsync<T>(
        T message,
        IEnumerable<IValidator<T>> validators,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validators);

        if (!validators.Any())
        {
            return [];
        }

        var context = new ValidationContext<T>(message);
        var failures = new List<ValidationFailure>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            if (result.Errors.Count != 0)
            {
                failures.AddRange(result.Errors.Where(e => e != null));
            }
        }

        return failures;
    }
}
