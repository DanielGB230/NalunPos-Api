using Pos.Domain.Authentication;
using Pos.Domain.Exceptions;
using Xunit;

namespace Pos.Domain.Tests;

/// <summary>
/// Pruebas del agregado <see cref="RefreshToken"/> (dominio puro, sin BD).
/// Cubre creación, validación de activos, revocación idempotente y rotación.
/// La vigencia absoluta de la familia tiene su propia suite en
/// <see cref="RefreshTokenFamilyExpiryTests"/>.
/// </summary>
public class RefreshTokenTests
{
    private static readonly TimeSpan s_familyAbsolute = TimeSpan.FromDays(30);

    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _familyId = Guid.CreateVersion7();
    private readonly string _tokenHash = "a".PadLeft(64, 'a'); // SHA-256 hex (64 chars)
    private readonly TimeSpan _lifetime = TimeSpan.FromDays(7);

    private RefreshToken NewToken(TimeSpan? lifetime = null) =>
        RefreshToken.CreateInitial(
            _userId,
            _tokenHash,
            _familyId,
            lifetime ?? _lifetime,
            s_familyAbsolute,
            _timeProvider);

    [Fact]
    public void CreateInitial_WithValidArguments_ShouldReturnActiveTokenWithUtcTimestamps()
    {
        // Act
        var token = NewToken();

        // Assert
        Assert.Equal(_userId, token.UserId);
        Assert.Equal(_tokenHash, token.TokenHash);
        Assert.Equal(_familyId, token.FamilyId);
        Assert.True(token.CreatedAtUtc.Kind == DateTimeKind.Utc);
        Assert.True(token.ExpiresAtUtc.Kind == DateTimeKind.Utc);
        Assert.True(token.FamilyExpiresAtUtc.Kind == DateTimeKind.Utc);
        Assert.Null(token.RevokedAtUtc);
        Assert.Null(token.ReplacedByTokenHash);
        Assert.Null(token.RevokedReason);
        Assert.True(token.ExpiresAtUtc > token.CreatedAtUtc);
        Assert.True(token.ExpiresAtUtc <= token.FamilyExpiresAtUtc);
        Assert.True(token.IsActive(token.CreatedAtUtc));
    }

    [Fact]
    public void CreateInitial_WithEmptyUserId_ShouldThrowDomainException()
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            RefreshToken.CreateInitial(Guid.Empty, _tokenHash, _familyId, _lifetime, s_familyAbsolute, _timeProvider));
        Assert.Contains("UserId", ex.Message);
    }

    [Fact]
    public void CreateInitial_WithEmptyFamilyId_ShouldThrowDomainException()
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            RefreshToken.CreateInitial(_userId, _tokenHash, Guid.Empty, _lifetime, s_familyAbsolute, _timeProvider));
        Assert.Contains("FamilyId", ex.Message);
    }

    [Fact]
    public void CreateInitial_WithEmptyTokenHash_ShouldThrowArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            RefreshToken.CreateInitial(_userId, string.Empty, _familyId, _lifetime, s_familyAbsolute, _timeProvider));
        Assert.Contains("token", ex.Message);
    }

    [Fact]
    public void CreateInitial_WithNonPositiveLifetime_ShouldThrowDomainException()
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            RefreshToken.CreateInitial(_userId, _tokenHash, _familyId, TimeSpan.Zero, s_familyAbsolute, _timeProvider));
        Assert.Contains("vigencia", ex.Message);
    }

    [Fact]
    public void IsActive_WhenNotRevokedAndNotExpired_ShouldReturnTrue()
    {
        // Arrange
        var token = NewToken();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Act
        var result = token.IsActive(now);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsActive_WhenRevoked_ShouldReturnFalse()
    {
        // Arrange
        var token = NewToken();
        token.Revoke(RefreshTokenRevocationReasons.Logout, _timeProvider);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Act
        var result = token.IsActive(now);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsActive_WhenExpired_ShouldReturnFalse()
    {
        // Arrange
        var token = NewToken(TimeSpan.FromSeconds(1));
        var expired = token.ExpiresAtUtc.AddSeconds(2);

        // Act
        var result = token.IsActive(expired);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsActive_WithNonUtcInstant_ShouldThrowDomainException()
    {
        // Arrange
        var token = NewToken();
        var local = DateTime.Now; // Kind = Local or Unspecified

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() => token.IsActive(local));
        Assert.Contains("UTC", ex.Message);
    }

    [Fact]
    public void Revoke_Once_ShouldSetRevokedAtUtcAndReason()
    {
        // Arrange
        var token = NewToken();
        var revokedAt = _timeProvider.GetUtcNow().UtcDateTime;

        // Act
        token.Revoke(RefreshTokenRevocationReasons.Rotated, _timeProvider);

        // Assert
        Assert.NotNull(token.RevokedAtUtc);
        Assert.True(token.RevokedAtUtc!.Value.Kind == DateTimeKind.Utc);
        Assert.Equal(RefreshTokenRevocationReasons.Rotated, token.RevokedReason);
    }

    [Fact]
    public void Revoke_Twice_ShouldBeIdempotentAndPreserveOriginalReason()
    {
        // Arrange
        var token = NewToken();
        token.Revoke(RefreshTokenRevocationReasons.Logout, _timeProvider);
        var firstRevokedAt = token.RevokedAtUtc;

        // Act
        token.Revoke(RefreshTokenRevocationReasons.ReuseDetected, _timeProvider);

        // Assert
        Assert.Equal(firstRevokedAt, token.RevokedAtUtc);
        Assert.Equal(RefreshTokenRevocationReasons.Logout, token.RevokedReason);
    }

    [Fact]
    public void Revoke_WithReplacedByTokenHash_ShouldRecordReplacement()
    {
        // Arrange
        var token = NewToken();
        string newHash = "b".PadLeft(64, 'b');

        // Act
        token.Revoke(RefreshTokenRevocationReasons.Rotated, _timeProvider, newHash);

        // Assert
        Assert.Equal(newHash, token.ReplacedByTokenHash);
    }
}
