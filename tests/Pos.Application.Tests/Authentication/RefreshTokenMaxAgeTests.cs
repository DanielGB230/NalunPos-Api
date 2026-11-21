using Pos.Application.Authentication;
using Pos.Application.Authentication.Commands.Login;
using Pos.Application.Authentication.Commands.RefreshToken;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Options;
using Pos.Application.Tests.Support.Fakes;
using Pos.Domain.Authentication;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Authentication;

/// <summary>
/// El Max-Age de la cookie se calcula desde el vencimiento EFECTIVO del token
/// emitido (puede estar recortado por la vigencia absoluta de la familia), nunca
/// desde las opciones y nunca por debajo de 1 segundo (F1.G.6).
/// </summary>
public class RefreshTokenMaxAgeTests
{
    private static readonly DateTimeOffset s_now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly string s_rawToken = "raw-refresh-token-for-max-age-0123456789";

    private readonly FakeAuthUserLookup _authUserLookup = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeTokenGenerator _tokenGenerator = new();
    private readonly FakeTenantStatusReader _tenantStatusReader = new();
    private readonly FakeRefreshTokenStore _refreshTokenStore = new();
    private readonly FakeRefreshTokenGenerator _refreshTokenGenerator = new();
    private readonly FixedTimeProvider _timeProvider = new(s_now);

    private static DateTime UtcNow => s_now.UtcDateTime;

    private static RefreshTokenOptions Options(int refreshTokenDays = 7, int familyAbsoluteDays = 30) =>
        new()
        {
            RefreshTokenDays = refreshTokenDays,
            FamilyAbsoluteDays = familyAbsoluteDays,
            RotationLeewaySeconds = 10
        };

    private User SeedActiveSuperAdmin()
    {
        var user = User.Create(
            new Email("maxage_unit@platform.test"),
            new PasswordHash(_passwordHasher.HashPassword("Password123!")),
            Role.SuperAdminRoleId,
            tenantId: null,
            firstName: "Max",
            lastName: "Age",
            timeProvider: TimeProvider.System);

        _authUserLookup.Users.Add(user);
        return user;
    }

    private LoginCommandHandler BuildLoginHandler(RefreshTokenOptions options) =>
        new(
            _authUserLookup,
            _passwordHasher,
            _tokenGenerator,
            _tenantStatusReader,
            _refreshTokenStore,
            _refreshTokenGenerator,
            _timeProvider,
            options);

    private RefreshTokenCommandHandler BuildRefreshHandler(RefreshTokenOptions options) =>
        new(
            _refreshTokenStore,
            _refreshTokenGenerator,
            _authUserLookup,
            _tokenGenerator,
            _tenantStatusReader,
            _timeProvider,
            options);

    private static string HashOf(string rawToken)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    [Fact]
    public async Task Login_MaxAge_ComesFromEffectiveExpiry()
    {
        _refreshTokenStore.TimeProvider = _timeProvider;
        var user = SeedActiveSuperAdmin();

        var result = await BuildLoginHandler(Options()).HandleAsync(
            new LoginCommand(user.Email.Value, "Password123!"));

        Assert.True(result.IsSuccess);
        Assert.Equal((int)TimeSpan.FromDays(7).TotalSeconds, result.Value.RefreshTokenMaxAgeSeconds);
    }

    [Fact]
    public async Task Login_MaxAge_IsClippedNearFamilyExpiry()
    {
        _refreshTokenStore.TimeProvider = _timeProvider;
        _refreshTokenStore.NextIssuedExpiresAtUtc = UtcNow.AddHours(1);
        var user = SeedActiveSuperAdmin();

        var result = await BuildLoginHandler(Options()).HandleAsync(
            new LoginCommand(user.Email.Value, "Password123!"));

        Assert.True(result.IsSuccess);
        Assert.Equal(3600, result.Value.RefreshTokenMaxAgeSeconds);
    }

    [Fact]
    public async Task Login_MaxAge_IsNeverBelowOneSecond()
    {
        _refreshTokenStore.TimeProvider = _timeProvider;
        _refreshTokenStore.NextIssuedExpiresAtUtc = UtcNow.AddMilliseconds(-500);
        var user = SeedActiveSuperAdmin();

        var result = await BuildLoginHandler(Options()).HandleAsync(
            new LoginCommand(user.Email.Value, "Password123!"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.RefreshTokenMaxAgeSeconds);
    }

    [Fact]
    public async Task Refresh_MaxAge_ComesFromEffectiveRotationExpiry()
    {
        var user = SeedActiveSuperAdmin();
        string tokenHash = HashOf(s_rawToken);
        _refreshTokenStore.SeedToken(
            user.Id,
            Guid.NewGuid(),
            tokenHash,
            createdAtUtc: UtcNow.AddHours(-1),
            expiresAtUtc: UtcNow.AddDays(7),
            familyExpiresAtUtc: UtcNow.AddDays(30));

        var result = await BuildRefreshHandler(Options()).HandleAsync(new RefreshTokenCommand(s_rawToken));

        Assert.True(result.IsSuccess);
        Assert.Equal((int)TimeSpan.FromDays(7).TotalSeconds, result.Value.RefreshTokenMaxAgeSeconds);
    }

    [Fact]
    public async Task Refresh_MaxAge_IsClippedByRemainingFamilyLifetime()
    {
        var user = SeedActiveSuperAdmin();
        string tokenHash = HashOf(s_rawToken);
        _refreshTokenStore.SeedToken(
            user.Id,
            Guid.NewGuid(),
            tokenHash,
            createdAtUtc: UtcNow.AddHours(-1),
            expiresAtUtc: UtcNow.AddMinutes(30),
            familyExpiresAtUtc: UtcNow.AddMinutes(30));

        var result = await BuildRefreshHandler(Options()).HandleAsync(new RefreshTokenCommand(s_rawToken));

        Assert.True(result.IsSuccess);
        Assert.Equal(1800, result.Value.RefreshTokenMaxAgeSeconds);
    }

    [Fact]
    public async Task Refresh_MaxAge_IsNeverBelowOneSecond()
    {
        var user = SeedActiveSuperAdmin();
        string tokenHash = HashOf(s_rawToken);
        _refreshTokenStore.SeedToken(
            user.Id,
            Guid.NewGuid(),
            tokenHash,
            createdAtUtc: UtcNow.AddMinutes(-1),
            expiresAtUtc: UtcNow.AddMilliseconds(500),
            familyExpiresAtUtc: UtcNow.AddMilliseconds(500));

        var result = await BuildRefreshHandler(Options()).HandleAsync(new RefreshTokenCommand(s_rawToken));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.RefreshTokenMaxAgeSeconds);
    }

    [Fact]
    public async Task Refresh_WhenFamilyAlreadyExpired_ReturnsInvalid_AndRevokesTokenAsExpired()
    {
        var user = SeedActiveSuperAdmin();
        string tokenHash = HashOf(s_rawToken);
        _refreshTokenStore.SeedToken(
            user.Id,
            Guid.NewGuid(),
            tokenHash,
            createdAtUtc: UtcNow.AddDays(-31),
            expiresAtUtc: UtcNow.AddSeconds(-1),
            familyExpiresAtUtc: UtcNow.AddSeconds(-1));

        var result = await BuildRefreshHandler(Options()).HandleAsync(new RefreshTokenCommand(s_rawToken));

        Assert.True(result.IsFailure);
        Assert.Equal(RefreshTokenErrors.InvalidCode, result.Error.Code);
        Assert.Equal(1, _refreshTokenStore.RevokeCallCount);
        Assert.Equal(0, _refreshTokenStore.RevokeFamilyCallCount);
        Assert.Equal(RefreshTokenRevocationReasons.Expired, _refreshTokenStore.GetRevokedReason(tokenHash));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
