using Pos.Domain.Authentication;
using Pos.Domain.Exceptions;
using Xunit;

namespace Pos.Domain.Tests;

/// <summary>
/// Pruebas de la vigencia absoluta de la familia de refresh tokens (F1.G.6).
/// El primer token fija <see cref="RefreshToken.FamilyExpiresAtUtc"/> y cada token
/// rotado la hereda sin recalcularla; la expiración deslizante nunca la supera.
/// </summary>
public class RefreshTokenFamilyExpiryTests
{
    private static readonly DateTimeOffset s_now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid s_userId = Guid.NewGuid();
    private static readonly Guid s_familyId = Guid.CreateVersion7();
    private static readonly string s_tokenHash = new('a', 64);
    private static readonly string s_rotatedHash = new('b', 64);
    private static readonly TimeSpan s_sliding = TimeSpan.FromDays(7);
    private static readonly TimeSpan s_absolute = TimeSpan.FromDays(30);

    [Fact]
    public void CreateInitial_SetsFamilyExpiryToCreatedPlusAbsolute_AndExpiresWithSliding()
    {
        var provider = new FixedTimeProvider(s_now);

        var token = RefreshToken.CreateInitial(
            s_userId,
            s_tokenHash,
            s_familyId,
            s_sliding,
            s_absolute,
            provider);

        Assert.Equal(s_now.UtcDateTime, token.CreatedAtUtc);
        Assert.Equal(s_now.UtcDateTime.Add(s_absolute), token.FamilyExpiresAtUtc);
        Assert.Equal(s_now.UtcDateTime.Add(s_sliding), token.ExpiresAtUtc);
        Assert.True(token.ExpiresAtUtc <= token.FamilyExpiresAtUtc);
        Assert.Equal(DateTimeKind.Utc, token.CreatedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, token.FamilyExpiresAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, token.ExpiresAtUtc.Kind);
    }

    [Fact]
    public void CreateInitial_WhenSlidingExceedsAbsolute_ClipsExpiresToFamilyExpiry()
    {
        var provider = new FixedTimeProvider(s_now);

        var token = RefreshToken.CreateInitial(
            s_userId,
            s_tokenHash,
            s_familyId,
            TimeSpan.FromDays(40),
            s_absolute,
            provider);

        Assert.Equal(s_now.UtcDateTime.Add(s_absolute), token.FamilyExpiresAtUtc);
        Assert.Equal(token.FamilyExpiresAtUtc, token.ExpiresAtUtc);
    }

    [Fact]
    public void CreateRotated_InheritsFamilyExpiry_AndUsesSlidingFromRotationInstant()
    {
        var initial = RefreshToken.CreateInitial(
            s_userId,
            s_tokenHash,
            s_familyId,
            s_sliding,
            s_absolute,
            new FixedTimeProvider(s_now));

        DateTimeOffset rotatedAt = s_now.AddDays(10);
        var rotated = RefreshToken.CreateRotated(
            initial,
            s_rotatedHash,
            s_sliding,
            new FixedTimeProvider(rotatedAt));

        Assert.Equal(initial.FamilyExpiresAtUtc, rotated.FamilyExpiresAtUtc);
        Assert.Equal(rotatedAt.UtcDateTime, rotated.CreatedAtUtc);
        Assert.Equal(rotatedAt.UtcDateTime.Add(s_sliding), rotated.ExpiresAtUtc);
        Assert.True(rotated.ExpiresAtUtc <= rotated.FamilyExpiresAtUtc);
        Assert.Equal(initial.UserId, rotated.UserId);
        Assert.Equal(initial.FamilyId, rotated.FamilyId);
    }

    [Fact]
    public void CreateRotated_WhenSlidingExceedsRemainingFamilyLifetime_ClipsExpiresToFamilyExpiry()
    {
        var initial = RefreshToken.CreateInitial(
            s_userId,
            s_tokenHash,
            s_familyId,
            s_sliding,
            s_absolute,
            new FixedTimeProvider(s_now));

        DateTimeOffset rotatedAt = s_now.AddDays(29);
        var rotated = RefreshToken.CreateRotated(
            initial,
            s_rotatedHash,
            s_sliding,
            new FixedTimeProvider(rotatedAt));

        Assert.Equal(initial.FamilyExpiresAtUtc, rotated.FamilyExpiresAtUtc);
        Assert.Equal(initial.FamilyExpiresAtUtc, rotated.ExpiresAtUtc);
    }

    [Fact]
    public void CreateInitial_WithNonPositiveAbsoluteLifetime_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            RefreshToken.CreateInitial(
                s_userId,
                s_tokenHash,
                s_familyId,
                s_sliding,
                TimeSpan.Zero,
                new FixedTimeProvider(s_now)));

        Assert.Contains("absoluta", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateInitial_WithNonPositiveSlidingLifetime_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            RefreshToken.CreateInitial(
                s_userId,
                s_tokenHash,
                s_familyId,
                TimeSpan.Zero,
                s_absolute,
                new FixedTimeProvider(s_now)));

        Assert.Contains("vigencia", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateRotated_WithNullCurrentToken_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RefreshToken.CreateRotated(
                null!,
                s_rotatedHash,
                s_sliding,
                new FixedTimeProvider(s_now)));
    }

    [Fact]
    public void CreateRotated_WithNonPositiveSlidingLifetime_ThrowsDomainException()
    {
        var initial = RefreshToken.CreateInitial(
            s_userId,
            s_tokenHash,
            s_familyId,
            s_sliding,
            s_absolute,
            new FixedTimeProvider(s_now));

        var ex = Assert.Throws<DomainException>(() =>
            RefreshToken.CreateRotated(
                initial,
                s_rotatedHash,
                TimeSpan.Zero,
                new FixedTimeProvider(s_now)));

        Assert.Contains("vigencia", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsActive_WithNonUtcInstant_StillThrowsDomainException()
    {
        var token = RefreshToken.CreateInitial(
            s_userId,
            s_tokenHash,
            s_familyId,
            s_sliding,
            s_absolute,
            new FixedTimeProvider(s_now));

        var ex = Assert.Throws<DomainException>(() => token.IsActive(DateTime.Now));
        Assert.Contains("UTC", ex.Message);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
