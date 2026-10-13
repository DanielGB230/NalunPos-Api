using System.Reflection;
using Pos.Application.Common.Attributes;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Validation;
using Pos.Domain.Common;

namespace Pos.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior CQRS que valida de forma centralizada y fail-closed que las solicitudes con
/// el atributo <see cref="RequiresTenantAttribute"/> cuenten con un tenant activo válido.
/// </summary>
public class TenantRequirementBehavior<TCommand, TResponse> : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private readonly ICommandHandler<TCommand, TResponse> _inner;
    private readonly ICurrentTenantContext _tenantContext;

    public static readonly DomainError TenantRequiredError = DomainError.Validation(
        "tenant.required",
        "Se requiere un contexto de tenant activo para ejecutar esta operación.");

    public TenantRequirementBehavior(
        ICommandHandler<TCommand, TResponse> inner,
        ICurrentTenantContext tenantContext)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public async Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool requiresTenant = typeof(TCommand).GetCustomAttribute<RequiresTenantAttribute>() != null;

        if (requiresTenant)
        {
            if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
            {
                if (ValidationResultFactory.IsResultResponse<TResponse>())
                {
                    return ValidationResultFactory.CreateResultForResponse<TResponse>(TenantRequiredError);
                }

                throw new InvalidOperationException(
                    "El comando requiere tenant activo pero no devuelve Result o Result<T>.");
            }
        }

        return await _inner.HandleAsync(command, cancellationToken);
    }
}
