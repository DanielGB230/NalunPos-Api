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
/// Emite los claims acordados: UserId (NameIdentifier), Email, Role (canónico para todos), y TenantId (si aplica).
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
            new(ClaimTypes.Role, roleName)
        };

        if (user.TenantId.HasValue)
        {
            claims.Add(new Claim("tenantId", user.TenantId.Value.ToString()));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(60), // Expiración corta (60 min)
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }

    public string GenerateToken(User user)
    {
        string roleName = user.RoleId == Role.SuperAdminRoleId || !user.TenantId.HasValue
            ? PlatformRoles.SuperAdmin
            : "User";
        return GenerateToken(user, roleName);
    }

    public string GenerateToken(User user, Role role)
    {
        ArgumentNullException.ThrowIfNull(role);
        return GenerateToken(user, role.Name);
    }
}
