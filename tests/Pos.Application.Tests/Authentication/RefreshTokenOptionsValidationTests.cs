using System.ComponentModel.DataAnnotations;
using Pos.Application.Common.Options;
using Xunit;

namespace Pos.Application.Tests.Authentication;

/// <summary>
/// Validación de <see cref="RefreshTokenOptions"/> (F1.G.6): la vigencia absoluta
/// de la familia debe ser configurable, tener rango y ser mayor o igual que la
/// vigencia deslizante del token.
/// </summary>
public class RefreshTokenOptionsValidationTests
{
    private static List<ValidationResult> Validate(RefreshTokenOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            options,
            new ValidationContext(options),
            results,
            validateAllProperties: true);
        return results;
    }

    [Fact]
    public void DefaultOptions_AreValid_AndUseThirtyDayAbsoluteLifetime()
    {
        var options = new RefreshTokenOptions();

        Assert.Equal(30, options.FamilyAbsoluteDays);
        Assert.Equal(TimeSpan.FromDays(30), options.FamilyAbsoluteLifetime);
        Assert.Empty(Validate(options));
    }

    [Fact]
    public void FamilyAbsoluteDays_LowerThanRefreshTokenDays_IsInvalid()
    {
        var options = new RefreshTokenOptions
        {
            RefreshTokenDays = 10,
            FamilyAbsoluteDays = 9
        };

        var results = Validate(options);

        Assert.NotEmpty(results);
        Assert.Contains(results, r =>
            r.ErrorMessage is not null &&
            r.ErrorMessage.Contains("FamilyAbsoluteDays", StringComparison.Ordinal) &&
            r.ErrorMessage.Contains("RefreshTokenDays", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public void FamilyAbsoluteDays_OutOfRange_IsInvalid(int familyAbsoluteDays)
    {
        var options = new RefreshTokenOptions
        {
            RefreshTokenDays = 7,
            FamilyAbsoluteDays = familyAbsoluteDays
        };

        Assert.NotEmpty(Validate(options));
    }

    [Fact]
    public void FamilyAbsoluteDays_EqualToRefreshTokenDays_IsValid()
    {
        var options = new RefreshTokenOptions
        {
            RefreshTokenDays = 7,
            FamilyAbsoluteDays = 7
        };

        Assert.Empty(Validate(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public void RefreshTokenDays_OutOfRange_IsInvalid(int refreshTokenDays)
    {
        var options = new RefreshTokenOptions
        {
            RefreshTokenDays = refreshTokenDays,
            FamilyAbsoluteDays = 30
        };

        Assert.NotEmpty(Validate(options));
    }
}
