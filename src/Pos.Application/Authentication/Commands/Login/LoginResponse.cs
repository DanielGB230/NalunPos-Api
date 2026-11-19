using System.Text.Json.Serialization;

namespace Pos.Application.Authentication.Commands.Login;

/// <summary>
/// DTO de respuesta tras un inicio de sesión exitoso.
/// El payload HTTP se mantiene plano: <c>RefreshToken</c> y
/// <c>RefreshTokenMaxAgeSeconds</c> están marcados con <see cref="JsonIgnoreAttribute"/>
/// para no serializarse; el controller los lee para escribir la cookie HttpOnly.
/// </summary>
public sealed record LoginResponse(
    string Token,
    Guid UserId,
    Guid RoleId,
    string Role,
    string Email,
    string FullName,
    Guid? TenantId,
    DateTime ExpiresAtUtc,
    [property: JsonIgnore] string? RefreshToken = null,
    [property: JsonIgnore] int RefreshTokenMaxAgeSeconds = 0);
