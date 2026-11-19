using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Pos.Application.Common.Interfaces;

namespace Pos.Infrastructure.Authentication;

/// <summary>
/// Implementación de <see cref="ICurrentUserClaims"/> que lee la identidad del
/// usuario autenticado de los claims del JWT en <see cref="HttpContext.User"/>.
/// No realiza consultas a BD (la identidad ya fue validada por el middleware de
/// autenticación).
/// </summary>
public class HttpContextCurrentUserClaims : ICurrentUserClaims
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserClaims(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public Guid? UserId => ParseGuid(ClaimTypes.NameIdentifier);

    public Guid? RoleId => ParseGuid("roleId");

    public string? Role => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role)?.Trim();

    public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);

    public string? FullName => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);

    public Guid? TenantId => ParseGuid("tenantId");

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    private Guid? ParseGuid(string? claimType)
    {
        if (string.IsNullOrWhiteSpace(claimType))
        {
            return null;
        }

        string? value = _httpContextAccessor.HttpContext?.User?.FindFirstValue(claimType!);
        if (string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value.Trim(), out var parsed))
        {
            return null;
        }
        return parsed;
    }
}
