using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class IdentityValueObjectTests
{
    [Theory]
    [InlineData("test@domain.com", "test@domain.com")]
    [InlineData("  USER@DOMAIN.COM  ", "user@domain.com")]
    [InlineData("First.Last@sub.domain.org", "first.last@sub.domain.org")]
    public void ValidEmailShouldBeNormalizedToLowercase(string input, string expected)
    {
        var email = new Email(input);
        Assert.Equal(expected, email.Value);
        Assert.Equal(expected, email.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-email")]
    [InlineData("user@")]
    [InlineData("@domain.com")]
    public void InvalidEmailFormatShouldThrowDomainException(string invalidEmail)
    {
        Assert.Throws<DomainException>(() => new Email(invalidEmail));
    }

    [Fact]
    public void EmptyPasswordHashShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => new PasswordHash(""));
        Assert.Throws<DomainException>(() => new PasswordHash("   "));
    }

    [Fact]
    public void ValidPasswordHashShouldBeEncapsulated()
    {
        var hash = new PasswordHash("ARGON2ID_SECRET_HASH");
        Assert.Equal("ARGON2ID_SECRET_HASH", hash.Value);
    }

    [Fact]
    public void EmptyCompanyTaxIdShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => new CompanyTaxId(""));
        Assert.Throws<DomainException>(() => new CompanyTaxId("123")); // Demasiado corto
    }

    [Fact]
    public void ValidCompanyTaxIdShouldBeNormalizedUppercase()
    {
        var taxId = new CompanyTaxId("  ruc-20123456789  ");
        Assert.Equal("RUC-20123456789", taxId.Value);
    }
}
