namespace Pos.Domain.Common;

/// <summary>
/// Define constantes y centinelas de alcance de plataforma del sistema.
///
/// Decisiones de Arquitectura:
/// - ADR 0008: Define <see cref="Guid.Empty"/> como centinela de plataforma/global en el filtro global de EF Core y RLS.
/// - ADR 0017: Establece <see cref="TenantId"/> (<see cref="Guid.Empty"/>) como el identificador representativo
///   para entidades que operan a nivel de plataforma (<see cref="IPlatformScopedEntity"/>, ej. Role, AuditLog)
///   cuando no hay un inquilino activo en contexto.
/// </summary>
public static class PlatformScope
{
    /// <summary>
    /// Identificador centinela que representa el ámbito de plataforma o alcance global sin inquilino.
    /// Equivale a <see cref="Guid.Empty"/> según lo estipulado en ADR 0008 y ADR 0017.
    /// </summary>
    public static readonly Guid TenantId = Guid.Empty;
}
