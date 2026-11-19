namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Portador inmutable de los claims de identidad resueltos del JWT.
/// Sirve como contrato de datos entre la infraestructura de lectura de claims
/// y los casos de uso que necesitan la identidad del usuario (ej. <c>/auth/me</c>).
/// </summary>
public sealed record CurrentUserClaims(
    Guid? UserId,
    Guid? RoleId,
    string? Role,
    string? Email,
    string? FullName,
    Guid? TenantId,
    bool IsAuthenticated) : ICurrentUserClaims;
