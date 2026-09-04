using Pos.Domain.Enums;

namespace Pos.Application.Authentication.Commands.Login;

/// <summary>
/// DTO de respuesta tras un inicio de sesión exitoso.
/// </summary>
public record LoginResponse(
    string Token,
    DateTime ExpiresAtUtc,
    Guid UserId,
    UserRole Role,
    Guid? TenantId
);
