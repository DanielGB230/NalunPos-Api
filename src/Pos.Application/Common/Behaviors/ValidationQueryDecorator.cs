using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Validation;
using Pos.Domain.Common;

namespace Pos.Application.Common.Behaviors;

/// <summary>
/// Decorador CQRS que ejecuta la validación con FluentValidation antes de invocar al IQueryHandler.
/// Usa <see cref="ValidationResultFactory"/> para construir el resultado cuando TResponse es Result o Result&lt;T&gt;.
/// </summary>
public class ValidationQueryDecorator<TQuery, TResponse> : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    private readonly IQueryHandler<TQuery, TResponse> _inner;
    private readonly IEnumerable<IValidator<TQuery>> _validators;

    public ValidationQueryDecorator(
        IQueryHandler<TQuery, TResponse> inner,
        IEnumerable<IValidator<TQuery>> validators)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _validators = validators ?? throw new ArgumentNullException(nameof(validators));
    }

    public async Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken = default)
    {
        var failures = await ValidationRunner.ValidateAsync(query, _validators, cancellationToken);

        if (failures.Count != 0)
        {
            if (ValidationResultFactory.IsResultResponse<TResponse>())
            {
                return ValidationResultFactory.CreateResultForResponse<TResponse>(failures);
            }

            throw new ValidationException(failures);
        }

        return await _inner.HandleAsync(query, cancellationToken);
    }
}
