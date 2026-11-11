using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Authentication;

/// <summary>
/// Generador de tokens JWT utilizando System.IdentityModel.Tokens.Jwt.
/// Emite los claims acordados: NameIdentifier, Email, Name, roleId, Role (canónico), y tenantId (únicamente para usuarios de tenant).
/// </summary>
public class JwtTokenGenerator : ITokenGenerator
{
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public JwtTokenGenerator(IConfiguration configuration, TimeProvider timeProvider)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public string GenerateToken(User user, string roleName)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        string normalizedRoleName = roleName.Trim();
        bool isSuperAdminRoleId = user.RoleId == Role.SuperAdminRoleId;
        bool isSuperAdminRoleName = string.Equals(normalizedRoleName, PlatformRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase);

        if (isSuperAdminRoleId || isSuperAdminRoleName)
        {
            if (!isSuperAdminRoleId || !isSuperAdminRoleName || user.TenantId.HasValue)
            {
                throw new InvalidOperationException(
                    "El usuario posee una combinación inconsistente de RoleId, roleName y TenantId para identidad de plataforma.");
            }
        }
        else if (!user.TenantId.HasValue)
        {
            throw new InvalidOperationException(
                "Un usuario de tenant debe tener un TenantId válido para emitir un token JWT.");
        }

        string secretKey = _configuration["JwtSettings:Secret"]
                           ?? throw new InvalidOperationException("JwtSettings:Secret is required.");
        string issuer = _configuration["JwtSettings:Issuer"]
                        ?? throw new InvalidOperationException("JwtSettings:Issuer is required.");
        string audience = _configuration["JwtSettings:Audience"]
                          ?? throw new InvalidOperationException("JwtSettings:Audience is required.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email.Value),
            new(ClaimTypes.Name, user.FullName),
            new("roleId", user.RoleId.ToString()),
            new(ClaimTypes.Role, normalizedRoleName)
        };

        if (user.TenantId.HasValue)
        {
            claims.Add(new Claim("tenantId", user.TenantId.Value.ToString()));
        }

        DateTime utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = utcNow,
            IssuedAt = utcNow,
            Expires = utcNow.AddMinutes(60), // Expiración corta (60 min)
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}
