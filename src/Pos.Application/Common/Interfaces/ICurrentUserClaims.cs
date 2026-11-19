namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Contrato para leer los claims de identidad del usuario autenticado desde el
/// contexto HTTP actual (sin consultas innecesarias a BD).
/// Implementado por <see cref="Pos.Infrastructure.Authentication.HttpContextCurrentUserClaims"/>.
/// </summary>
public interface ICurrentUserClaims
{
    Guid? UserId { get; }
    Guid? RoleId { get; }
    string? Role { get; }
    string? Email { get; }
    string? FullName { get; }
    Guid? TenantId { get; }
    bool IsAuthenticated { get; }
}
