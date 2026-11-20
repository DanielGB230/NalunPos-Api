using Pos.Application.Authentication;
using Pos.Application.Authentication.Commands.RefreshToken;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Options;
using Pos.Application.Tests.Support.Fakes;
using Pos.Domain.Authentication;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Authentication;

/// <summary>
/// Pruebas unitarias de <see cref="RefreshTokenCommandHandler"/> con el fake del store.
/// Cubren la ventana de tolerancia (F1.G.5): una rotación concurrente dentro de la
/// ventana devuelve 409 sin revocar la familia ni emitir tokens; un reuso real
/// (fuera de la ventana o con otro motivo) revoca toda la familia.
/// </summary>
public class RefreshTokenCommandHandlerTests
{
    private static readonly DateTimeOffset s_fixedNow = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly string s_rawToken = "raw-refresh-token-for-tests-0123456789";

    private readonly FakeRefreshTokenStore _refreshTokenStore = new();
    private readonly FakeRefreshTokenGenerator _refreshTokenGenerator = new();
    private readonly FakeAuthUserLookup _authUserLookup = new();
    private readonly FakeTokenGenerator _tokenGenerator = new();
    private readonly FakeTenantStatusReader _tenantStatusReader = new();
    private readonly FixedTimeProvider _timeProvider = new(s_fixedNow);
    private readonly RefreshTokenOptions _options = new() { RefreshTokenDays = 7, RotationLeewaySeconds = 10 };
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        _handler = new RefreshTokenCommandHandler(
            _refreshTokenStore,
            _refreshTokenGenerator,
            _authUserLookup,
            _tokenGenerator,
            _tenantStatusReader,
            _timeProvider,
            _options);
    }

    private static DateTime UtcNow => s_fixedNow.UtcDateTime;

    private User SeedActiveSuperAdmin()
    {
        var user = User.Create(
            new Email("refresh_unit@platform.test"),
            new PasswordHash("hashed-password"),
            Role.SuperAdminRoleId,
            tenantId: null,
            firstName: "Refresh",
            lastName: "Unit",
            timeProvider: TimeProvider.System);

        _authUserLookup.Users.Add(user);
        return user;
    }

    private User SeedTenantAdmin(Guid tenantId)
    {
        Guid roleId = Guid.NewGuid();
        var user = User.Create(
            new Email($"refresh_unit_{Guid.NewGuid():N}@tenant.test"),
            new PasswordHash("hashed-password"),
            roleId,
            tenantId,
            firstName: "Refresh",
            lastName: "Tenant",
            timeProvider: TimeProvider.System);

        _authUserLookup.Users.Add(user);
        _authUserLookup.SetRoleName(roleId, TenantRoles.TenantAdmin);
        return user;
    }

    private void SeedToken(
        User user,
        string tokenHash,
        DateTime? expiresAtUtc = null,
        string? revokedReason = null,
        DateTime? revokedAtUtc = null)
    {
        _refreshTokenStore.SeedToken(
            user.Id,
            Guid.NewGuid(),
            tokenHash,
            createdAtUtc: UtcNow.AddHours(-1),
            expiresAtUtc: expiresAtUtc ?? UtcNow.AddDays(7),
            revokedReason,
            revokedAtUtc);
    }

    private static string HashOf(string rawToken)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static RefreshTokenCommand Command(string? rawToken = null) => new(rawToken ?? s_rawToken);

    [Fact]
    public async Task HandleAsync_WithEmptyToken_ShouldReturnInvalidWithoutTouchingStore()
    {
        var result = await _handler.HandleAsync(new RefreshTokenCommand("   "));

        Assert.True(result.IsFailure);
        Assert.Equal(RefreshTokenErrors.InvalidCode, result.Error.Code);
        Assert.Equal(0, _refreshTokenStore.RotateCallCount);
        Assert.Equal(0, _refreshTokenStore.RevokeFamilyCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithUnknownToken_ShouldReturnInvalidWithoutRevokingFamily()
    {
        var result = await _handler.HandleAsync(Command("unknown-token"));

        Assert.True(result.IsFailure);
        Assert.Equal(RefreshTokenErrors.InvalidCode, result.Error.Code);
        Assert.Equal(0, _refreshTokenStore.RevokeFamilyCallCount);
        Assert.Equal(0, _refreshTokenStore.RotateCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithRotatedTokenInsideLeeway_ShouldReturnConflictWithoutRevokingFamily()
    {
        var user = SeedActiveSuperAdmin();
        string tokenHash = HashOf(s_rawToken);
        SeedToken(user, tokenHash, revokedReason: RefreshTokenRevocationReasons.Rotated, revokedAtUtc: UtcNow.AddSeconds(-5));

        var result = await _handler.HandleAsync(Command());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(RefreshTokenErrors.ConcurrentRotationCode, result.Error.Code);
        Assert.Equal(0, _refreshTokenStore.RevokeFamilyCallCount);
        Assert.Equal(0, _refreshTokenStore.RotateCallCount);
        Assert.Equal(1, _refreshTokenStore.TokenCount);
    }

    [Fact]
    public async Task HandleAsync_WithRotatedTokenOutsideLeeway_ShouldRevokeFamily()
    {
        var user = SeedActiveSuperAdmin();
        string tokenHash = HashOf(s_rawToken);
        SeedToken(user, tokenHash, revokedReason: RefreshTokenRevocationReasons.Rotated, revokedAtUtc: UtcNow.AddSeconds(-11));

        var result = await _handler.HandleAsync(Command());

        Assert.True(result.IsFailure);
        Assert.Equal(RefreshTokenErrors.InvalidCode, result.Error.Code);
        Assert.Equal(1, _refreshTokenStore.RevokeFamilyCallCount);
        Assert.Equal(RefreshTokenRevocationReasons.ReuseDetected, _refreshTokenStore.LastRevokeFamilyReason);
    }

    [Fact]
    public async Task HandleAsync_WithLogoutRevokedTokenInsideLeeway_ShouldRevokeFamily()
    {
        var user = SeedActiveSuperAdmin();
        string tokenHash = HashOf(s_rawToken);
        SeedToken(user, tokenHash, revokedReason: RefreshTokenRevocationReasons.Logout, revokedAtUtc: UtcNow.AddSeconds(-1));

        var result = await _handler.HandleAsync(Command());

        Assert.True(result.IsFailure);
        Assert.Equal(RefreshTokenErrors.InvalidCode, result.Error.Code);
        Assert.Equal(1, _refreshTokenStore.RevokeFamilyCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithExpiredToken_ShouldRevokeTokenOnly()
    {
        var user = SeedActiveSuperAdmin();
        string tokenHash = HashOf(s_rawToken);
        SeedToken(user, tokenHash, expiresAtUtc: UtcNow.AddSeconds(-1));

        var result = await _handler.HandleAsync(Command());

        Assert.True(result.IsFailure);
        Assert.Equal(RefreshTokenErrors.InvalidCode, result.Error.Code);
        Assert.Equal(1, _refreshTokenStore.RevokeCallCount);
        Assert.Equal(0, _refreshTokenStore.RevokeFamilyCallCount);
        Assert.Equal(RefreshTokenRevocationReasons.Expired, _refreshTokenStore.GetRevokedReason(tokenHash));
    }

    [Fact]
    public async Task HandleAsync_WithInactiveUser_ShouldNotConsumeToken()
    {
        var user = SeedActiveSuperAdmin();
        user.Deactivate(TimeProvider.System);
        string tokenHash = HashOf(s_rawToken);
        SeedToken(user, tokenHash);

        var result = await _handler.HandleAsync(Command());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Equal("Auth.UserInvalid", result.Error.Code);
        Assert.Equal(0, _refreshTokenStore.RotateCallCount);
        Assert.Null(_refreshTokenStore.GetRevokedReason(tokenHash));
    }

    [Fact]
    public async Task HandleAsync_WithSuspendedTenant_ShouldNotConsumeToken()
    {
        Guid tenantId = Guid.NewGuid();
        var user = SeedTenantAdmin(tenantId);
        _tenantStatusReader.SetStatus(tenantId, TenantStatus.Suspended);
        string tokenHash = HashOf(s_rawToken);
        SeedToken(user, tokenHash);

        var result = await _handler.HandleAsync(Command());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal(TenantAccessPolicy.SuspendedCode, result.Error.Code);
        Assert.Equal(0, _refreshTokenStore.RotateCallCount);
        Assert.Null(_refreshTokenStore.GetRevokedReason(tokenHash));
    }

    [Fact]
    public async Task HandleAsync_WithActiveTokenAndUser_ShouldRotateAndEmitNewToken()
    {
        var user = SeedActiveSuperAdmin();
        string tokenHash = HashOf(s_rawToken);
        SeedToken(user, tokenHash);

        var result = await _handler.HandleAsync(Command());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(FakeRefreshTokenGenerator.FixedToken, result.Value.RefreshToken);
        Assert.Equal(RefreshTokenRevocationReasons.Rotated, _refreshTokenStore.GetRevokedReason(tokenHash));
        Assert.Equal(0, _refreshTokenStore.RevokeFamilyCallCount);
        Assert.Equal(2, _refreshTokenStore.TokenCount);
    }

    [Fact]
    public async Task HandleAsync_WhenRotationLosesRaceInsideLeeway_ShouldReturnConflictWithoutRevokingFamily()
    {
        var user = SeedActiveSuperAdmin();
        string tokenHash = HashOf(s_rawToken);
        SeedToken(user, tokenHash);

        _refreshTokenStore.SimulateRotationRace = new Pos.Application.Common.Interfaces.RefreshTokenCandidate(
            tokenHash,
            user.Id,
            Guid.NewGuid(),
            IsRevoked: true,
            RevokedAtUtc: UtcNow.AddSeconds(-3),
            ExpiresAtUtc: UtcNow.AddDays(7),
            RevokedReason: RefreshTokenRevocationReasons.Rotated);

        var result = await _handler.HandleAsync(Command());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(RefreshTokenErrors.ConcurrentRotationCode, result.Error.Code);
        Assert.Equal(0, _refreshTokenStore.RevokeFamilyCallCount);
        Assert.Equal(1, _refreshTokenStore.TokenCount);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
