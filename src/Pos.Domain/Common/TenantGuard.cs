using Pos.Domain.Exceptions;

namespace Pos.Domain.Common;

/// <summary>
/// Guardián centralizado para validar la invariante de pertenencia a un inquilino (Tenant)
/// en la creación y mutación de entidades que implementan <see cref="ITenantOwnedEntity"/>.
///
/// Decisiones de Arquitectura:
/// - ADR 0008: Define aislamiento estricto multi-tenant donde entidades de inquilino no pueden poseer alcance global.
/// - ADR 0017: Establece que ninguna entidad de tenant puede nacer con <see cref="PlatformScope.TenantId"/>
///   (valor centinela equivalente a <see cref="Guid.Empty"/>).
/// </summary>
public static class TenantGuard
{
    /// <summary>
    /// Valida que el identificador de tenant sea válido y no coincida con el centinela de plataforma.
    /// Lanza <see cref="DomainException"/> si la validación falla.
    /// </summary>
    /// <param name="tenantId">Identificador del tenant a validar.</param>
    /// <param name="entityDescription">Descripción de la entidad para el mensaje (ej: "una sucursal", "una caja").</param>
    /// <returns>El identificador de tenant validado.</returns>
    public static Guid RequireTenantId(Guid tenantId, string entityDescription)
    {
        if (tenantId == PlatformScope.TenantId)
        {
            throw new DomainException($"El ID del tenant es requerido para crear {entityDescription}.");
        }

        return tenantId;
    }
}
