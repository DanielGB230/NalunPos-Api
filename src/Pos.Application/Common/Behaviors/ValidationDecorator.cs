using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Validation;
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
            if (typeof(TResponse) == typeof(Result) ||
                (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>)))
            {
                return ValidationResultFactory.CreateResultForResponse<TResponse>(failures);
            }

            throw new ValidationException(failures);
        }

        return await _inner.HandleAsync(command, cancellationToken);
    }
}
