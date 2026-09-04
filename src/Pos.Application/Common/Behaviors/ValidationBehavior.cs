using FluentValidation;
using Pos.Application.Common.Interfaces;

namespace Pos.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior de validación — reemplaza MediatR.IPipelineBehavior.
/// Se ejecuta antes del handler y lanza ValidationException si hay fallos.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ICommandHandler<TRequest, TResponse> _inner;

    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ICommandHandler<TRequest, TResponse> inner)
    {
        _validators = validators;
        _inner = inner;
    }

    public async Task<TResponse> HandleAsync(TRequest command, CancellationToken cancellationToken = default)
    {
        if (!_validators.Any())
        {
            return await _inner.HandleAsync(command, cancellationToken);
        }

        var context = new ValidationContext<TRequest>(command);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await _inner.HandleAsync(command, cancellationToken);
    }
}
