using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Authentication;
using Xunit;

namespace Pos.Infrastructure.Tests;

public class JwtTokenGeneratorTests
{
    private const string TestIssuer = "NalunPosApi.Tests";
    private const string TestAudience = "NalunPosClients.Tests";
    private static readonly string TestSigningKey = new('A', 64);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static JwtTokenGenerator CreateGenerator(DateTimeOffset? utcNow = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = TestSigningKey,
                ["JwtSettings:Issuer"] = TestIssuer,
                ["JwtSettings:Audience"] = TestAudience
            })
            .Build();

        return new JwtTokenGenerator(
            config,
            new FixedTimeProvider(utcNow ?? new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero)));
    }

    private static ClaimsPrincipal ValidateAndDecode(string token, DateTimeOffset referenceTime)
    {
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey)),
            ValidateIssuer = true,
            ValidIssuer = TestIssuer,
            ValidateAudience = true,
            ValidAudience = TestAudience,
            ValidateLifetime = true,
            LifetimeValidator = (notBefore, expires, _, _) =>
                (!notBefore.HasValue || notBefore.Value <= referenceTime.UtcDateTime) &&
                expires.HasValue &&
                expires.Value > referenceTime.UtcDateTime
        };

        return handler.ValidateToken(token, parameters, out _);
    }

    [Fact]
    public void GenerateToken_WhenUserIsNull_ThrowsArgumentNullException()
    {
        var generator = CreateGenerator();

        Assert.Throws<ArgumentNullException>(() => generator.GenerateToken(null!, PlatformRoles.SuperAdmin));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateToken_WhenRoleNameIsNullEmptyOrWhitespace_ThrowsArgumentException(string? invalidRoleName)
    {
        var generator = CreateGenerator();
        var tenantUser = User.Create(
            new Email("cajero@tenant.test"),
            new PasswordHash("hashed-password"),
            Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            "Ana",
            "Lopez",
            TimeProvider.System);

        Assert.ThrowsAny<ArgumentException>(() => generator.GenerateToken(tenantUser, invalidRoleName!));
    }

    [Fact]
    public void GenerateToken_ForSuperAdmin_EmitsExactContractClaimsWithoutTenantId()
    {
        var now = new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
        var generator = CreateGenerator(now);
        var superAdmin = User.Create(
            new Email("superadmin@platform.test"),
            new PasswordHash("hashed-password"),
            Role.SuperAdminRoleId,
            tenantId: null,
            "Super",
            "Admin",
            TimeProvider.System);

        string token = generator.GenerateToken(superAdmin, PlatformRoles.SuperAdmin);
        Assert.False(string.IsNullOrWhiteSpace(token));

        var principal = ValidateAndDecode(token, now);

        Assert.Equal(superAdmin.Id.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("superadmin@platform.test", principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("Super Admin", principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal(Role.SuperAdminRoleId.ToString(), principal.FindFirstValue("roleId"));
        Assert.Equal(PlatformRoles.SuperAdmin, principal.FindFirstValue(ClaimTypes.Role));

        // Jamás debe emitir claims de tenant para SuperAdmin
        Assert.Null(principal.FindFirst("tenantId"));
        Assert.Null(principal.FindFirst("tenant_id"));
        Assert.Null(principal.FindFirst("tid"));

        // Verificar claims del payload JWT crudo y expiración de 60 minutos
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(now.UtcDateTime.AddMinutes(60), jwt.ValidTo);
        var expectedJwtTypes = new[] { "nameid", "email", "unique_name", "roleId", "role", "nbf", "exp", "iat", "iss", "aud" };
        Assert.Equal(
            expectedJwtTypes.OrderBy(x => x, StringComparer.Ordinal),
            jwt.Claims.Select(c => c.Type).OrderBy(x => x, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(TenantRoles.TenantAdmin)]
    [InlineData(TenantRoles.Supervisor)]
    [InlineData(TenantRoles.InventoryManager)]
    [InlineData(TenantRoles.Cajero)]
    public void GenerateToken_ForTenantUser_EmitsExactContractClaimsIncludingTenantId(string canonicalRole)
    {
        var now = new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
        var generator = CreateGenerator(now);
        Guid tenantId = Guid.NewGuid();
        Guid roleId = Guid.NewGuid();
        var user = User.Create(
            new Email("operador@empresa.test"),
            new PasswordHash("hashed-password"),
            roleId,
            tenantId,
            "Carlos",
            "Ramos",
            TimeProvider.System);

        string token = generator.GenerateToken(user, canonicalRole);
        Assert.False(string.IsNullOrWhiteSpace(token));

        var principal = ValidateAndDecode(token, now);

        Assert.Equal(user.Id.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("operador@empresa.test", principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("Carlos Ramos", principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal(roleId.ToString(), principal.FindFirstValue("roleId"));
        Assert.Equal(canonicalRole, principal.FindFirstValue(ClaimTypes.Role));
        Assert.Equal(tenantId.ToString(), principal.FindFirstValue("tenantId"));

        // Verificar exactamente los claims emitidos en el JWT
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(now.UtcDateTime.AddMinutes(60), jwt.ValidTo);
        var expectedJwtTypes = new[] { "nameid", "email", "unique_name", "roleId", "role", "tenantId", "nbf", "exp", "iat", "iss", "aud" };
        Assert.Equal(
            expectedJwtTypes.OrderBy(x => x, StringComparer.Ordinal),
            jwt.Claims.Select(c => c.Type).OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void GenerateToken_WhenSuperAdminRoleIdHasTenantId_ThrowsInvalidOperationException()
    {
        var generator = CreateGenerator();
        var inconsistentUser = User.Create(
            new Email("anomaly@tenant.test"),
            new PasswordHash("hashed-password"),
            Role.SuperAdminRoleId,
            tenantId: Guid.NewGuid(),
            "Fake",
            "SuperAdmin",
            TimeProvider.System);

        Assert.Throws<InvalidOperationException>(() => generator.GenerateToken(inconsistentUser, PlatformRoles.SuperAdmin));
    }

    [Fact]
    public void GenerateToken_WhenTenantUserClaimsSuperAdminRoleName_ThrowsInvalidOperationException()
    {
        var generator = CreateGenerator();
        var tenantUser = User.Create(
            new Email("escalated@tenant.test"),
            new PasswordHash("hashed-password"),
            Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            "Tenant",
            "User",
            TimeProvider.System);

        Assert.Throws<InvalidOperationException>(() => generator.GenerateToken(tenantUser, PlatformRoles.SuperAdmin));
    }

    [Fact]
    public void GenerateToken_WhenNonSuperAdminUserHasNullTenantId_ThrowsInvalidOperationException()
    {
        var generator = CreateGenerator();
        var orphanUser = User.Create(
            new Email("orphan@platform.test"),
            new PasswordHash("hashed-password"),
            Guid.NewGuid(),
            tenantId: null,
            "Orphan",
            "User",
            TimeProvider.System);

        Assert.Throws<InvalidOperationException>(() => generator.GenerateToken(orphanUser, TenantRoles.Cajero));
    }
}

