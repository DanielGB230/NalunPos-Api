using Pos.Infrastructure.Authentication;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Infrastructure.Tests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ShouldReturnArgon2idPhcFormattedString()
    {
        // Arrange
        string password = "SuperSecretPassword123!";

        // Act
        string hash = _hasher.HashPassword(password);

        // Assert
        Assert.NotNull(hash);
        Assert.StartsWith("$argon2id$v=19$m=65536,t=3,p=1$", hash);
        Assert.NotEqual(password, hash);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        string password = "SuperSecretPassword123!";
        string hash = _hasher.HashPassword(password);

        // Act
        bool isValid = _hasher.VerifyPassword(password, hash);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        string password = "SuperSecretPassword123!";
        string hash = _hasher.HashPassword(password);

        // Act
        bool isValid = _hasher.VerifyPassword("WrongPassword!", hash);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Verify_WithPasswordHashValueObject_ShouldReturnTrueForCorrectPassword()
    {
        // Arrange
        string password = "PasswordWithValueObject123!";
        string hashString = _hasher.HashPassword(password);
        var passwordHashVo = new PasswordHash(hashString);

        // Act
        bool isValid = _hasher.Verify(password, passwordHashVo);

        // Assert
        Assert.True(isValid);
    }
}
