namespace Pos.Domain.Common;

/// <summary>
/// Interfaz marcador que indica que una entidad pertenece a un tenant específico.
///
/// REGLA ARQUITECTÓNICA (v3 — Multi-tenancy):
/// - Toda entidad de negocio que almacena datos de un tenant DEBE implementar esta interfaz.
/// - Las entidades de plataforma (Tenant, Plan, Subscription) NO la implementan,
///   ya que existen a nivel de plataforma, no dentro de un tenant.
///
/// RESPONSABILIDAD DE CADA CAPA:
/// - Domain: declara el contrato ("esta entidad tiene TenantId") sin conocer cómo se resuelve.
/// - Infrastructure: aplica automáticamente el TenantId via TenantSaveChangesInterceptor
///   al crear entidades, y filtra via EF Core Global Query Filters al leer.
/// - Application: jamás asigna TenantId manualmente; confía en Infrastructure.
/// - Api: jamás acepta TenantId del cliente; se resuelve exclusivamente del JWT
///   via TenantResolutionMiddleware.
///
/// Ver: ADR 0008 — Estrategia de Aislamiento de Tenant
/// </summary>
public interface ITenantOwnedEntity
{
    Guid TenantId { get; }
}
