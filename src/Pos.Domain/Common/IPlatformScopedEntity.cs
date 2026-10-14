namespace Pos.Domain.Common;

/// <summary>
/// Contrato de entidad perteneciente al inquilino que puede existir en el alcance de plataforma
/// (<see cref="ITenantOwnedEntity.TenantId"/> == <see cref="Guid.Empty"/>) cuando no hay un inquilino
/// activo en el contexto de ejecución.
///
/// Decisión de Arquitectura:
/// - ADR 0008: Define <see cref="Guid.Empty"/> como centinela de plataforma/global en el filtro global y RLS.
/// - ADR 0017: Lista cerrada de entidades autorizadas a operar en ámbito de plataforma (<c>Role</c> y <c>AuditLog</c>).
///   Cualquier otra entidad perteneciente al inquilino sin contexto activo es rechazada por el interceptor fail-closed.
/// </summary>
public interface IPlatformScopedEntity : ITenantOwnedEntity
{
}
