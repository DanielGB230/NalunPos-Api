namespace Pos.Domain.Common;

/// <summary>
/// Interfaz para entidades que pueden o no pertenecer a un tenant.
/// Útil para entidades como User, donde un SuperAdmin no pertenece a ninguno.
/// </summary>
public interface IOptionalTenantOwnedEntity
{
    Guid? TenantId { get; }
}
