namespace Pos.Application.Authentication.Queries.GetCurrentUser;

/// <summary>
/// Contrato de respuesta de <c>GET /auth/me</c>.
/// Contiene la identidad exacta resuelta de los claims del JWT (sin consulta a BD).
/// </summary>
public sealed record CurrentUserResponse(
    Guid UserId,
    Guid RoleId,
    string Role,
    string Email,
    string FullName,
    Guid? TenantId);
