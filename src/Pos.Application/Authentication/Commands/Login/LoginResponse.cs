namespace Pos.Application.Authentication.Commands.Login;

/// <summary>
/// DTO de respuesta tras un inicio de sesión exitoso.
/// </summary>
public sealed record LoginResponse(
    string Token,
    Guid UserId,
    Guid RoleId,
    string Role,
    string Email,
    string FullName,
    Guid? TenantId,
    DateTime ExpiresAtUtc
);
