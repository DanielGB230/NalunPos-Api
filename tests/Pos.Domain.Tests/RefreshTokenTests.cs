using Pos.Domain.Authentication;
using Pos.Domain.Exceptions;
using Xunit;

namespace Pos.Domain.Tests;

/// <summary>
/// Pruebas del agregado <see cref="RefreshToken"/> (dominio puro, sin BD).
/// Cubre creación, validación de activos, revocación idempotente y rotación.
/// </summary>
public class RefreshTokenTests
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _familyId = Guid.CreateVersion7();
    private readonly string _tokenHash = "a".PadLeft(64, 'a'); // SHA-256 hex (64 chars)
    private readonly TimeSpan _lifetime = TimeSpan.FromDays(7);

    [Fact]
    public void Create_WithValidArguments_ShouldReturnActiveTokenWithUtcTimestamps()
    {
        // Act
        var token = RefreshToken.Create(_userId, _tokenHash, _familyId, _lifetime, _timeProvider);

        // Assert
        Assert.Equal(_userId, token.UserId);
        Assert.Equal(_tokenHash, token.TokenHash);
        Assert.Equal(_familyId, token.FamilyId);
        Assert.True(token.CreatedAtUtc.Kind == DateTimeKind.Utc);
        Assert.True(token.ExpiresAtUtc.Kind == DateTimeKind.Utc);
        Assert.Null(token.RevokedAtUtc);
        Assert.Null(token.ReplacedByTokenHash);
        Assert.Null(token.RevokedReason);
        Assert.True(token.ExpiresAtUtc > token.CreatedAtUtc);
        Assert.True(token.IsActive(token.CreatedAtUtc));
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldThrowDomainException()
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            RefreshToken.Create(Guid.Empty, _tokenHash, _familyId, _lifetime, _timeProvider));
        Assert.Contains("UserId", ex.Message);
    }

    [Fact]
    public void Create_WithEmptyFamilyId_ShouldThrowDomainException()
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            RefreshToken.Create(_userId, _tokenHash, Guid.Empty, _lifetime, _timeProvider));
        Assert.Contains("FamilyId", ex.Message);
    }

    [Fact]
    public void Create_WithEmptyTokenHash_ShouldThrowArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            RefreshToken.Create(_userId, string.Empty, _familyId, _lifetime, _timeProvider));
        Assert.Contains("token", ex.Message);
    }

    [Fact]
    public void Create_WithNonPositiveLifetime_ShouldThrowDomainException()
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            RefreshToken.Create(_userId, _tokenHash, _familyId, TimeSpan.Zero, _timeProvider));
        Assert.Contains("vigencia", ex.Message);
    }

    [Fact]
    public void IsActive_WhenNotRevokedAndNotExpired_ShouldReturnTrue()
    {
        // Arrange
        var token = RefreshToken.Create(_userId, _tokenHash, _familyId, _lifetime, _timeProvider);
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
        var token = RefreshToken.Create(_userId, _tokenHash, _familyId, _lifetime, _timeProvider);
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
        var token = RefreshToken.Create(_userId, _tokenHash, _familyId, TimeSpan.FromSeconds(1), _timeProvider);
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
        var token = RefreshToken.Create(_userId, _tokenHash, _familyId, _lifetime, _timeProvider);
        var local = DateTime.Now; // Kind = Local or Unspecified

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() => token.IsActive(local));
        Assert.Contains("UTC", ex.Message);
    }

    [Fact]
    public void Revoke_Once_ShouldSetRevokedAtUtcAndReason()
    {
        // Arrange
        var token = RefreshToken.Create(_userId, _tokenHash, _familyId, _lifetime, _timeProvider);
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
        var token = RefreshToken.Create(_userId, _tokenHash, _familyId, _lifetime, _timeProvider);
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
        var token = RefreshToken.Create(_userId, _tokenHash, _familyId, _lifetime, _timeProvider);
        string newHash = "b".PadLeft(64, 'b');

        // Act
        token.Revoke(RefreshTokenRevocationReasons.Rotated, _timeProvider, newHash);

        // Assert
        Assert.Equal(newHash, token.ReplacedByTokenHash);
    }
}