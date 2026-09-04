using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;

namespace Pos.Application.Common.Behaviors;

/// <summary>
/// Decorador CQRS que ejecuta la validación con FluentValidation antes de invocar al ICommandHandler.
/// </summary>
public class ValidationDecorator<TCommand, TResponse> : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private readonly ICommandHandler<TCommand, TResponse> _inner;
    private readonly IEnumerable<IValidator<TCommand>> _validators;

    public ValidationDecorator(
        ICommandHandler<TCommand, TResponse> inner,
        IEnumerable<IValidator<TCommand>> validators)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _validators = validators ?? throw new ArgumentNullException(nameof(validators));
    }

    public async Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        if (!_validators.Any())
        {
            return await _inner.HandleAsync(command, cancellationToken);
        }

        var context = new ValidationContext<TCommand>(command);
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
        {
            var errorMessage = string.Join("; ", failures.Select(f => f.ErrorMessage));
            var error = DomainError.Validation("Validation.Error", errorMessage);

            if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
            {
                var resultType = typeof(TResponse);
                var valueType = resultType.GetGenericArguments()[0];
                var failMethod = typeof(Result)
                    .GetMethod(nameof(Result.Fail), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(valueType);

                return (TResponse)failMethod.Invoke(null, [error])!;
            }

            if (typeof(TResponse) == typeof(Result))
            {
                return (TResponse)(object)Result.Failure(error);
            }

            throw new ValidationException(failures);
        }

        return await _inner.HandleAsync(command, cancellationToken);
    }
}
