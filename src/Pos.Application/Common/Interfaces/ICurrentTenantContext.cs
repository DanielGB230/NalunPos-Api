namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Abstracción para acceder al contexto del tenant actual durante la ejecución de la solicitud.
/// </summary>
public interface ICurrentTenantContext
{
    Guid? TenantId { get; }
    bool IsSuperAdmin { get; }
    bool HasTenant { get; }
}
