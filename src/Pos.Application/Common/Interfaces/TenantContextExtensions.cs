using Pos.Domain.Common;

namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Métodos de extensión para defensiva en profundidad sobre <see cref="ICurrentTenantContext"/>.
/// </summary>
public static class TenantContextExtensions
{
    /// <summary>
    /// Obtiene el identificador de tenant actual de manera obligatoria.
    /// Lanza <see cref="InvalidOperationException"/> si el contexto no cuenta con un tenant válido o si es Guid.Empty.
    /// </summary>
    /// <param name="context">El contexto de tenant actual.</param>
    /// <returns>El identificador Guid del tenant.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="context"/> es nulo.</exception>
    /// <exception cref="InvalidOperationException">Si el tenant no está resuelto o es PlatformScope.TenantId.</exception>
    public static Guid RequireTenantId(this ICurrentTenantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.TenantId.HasValue || context.TenantId.Value == PlatformScope.TenantId)
        {
            throw new InvalidOperationException("TenantId is required but was not resolved in the current context.");
        }

        return context.TenantId.Value;
    }
}
