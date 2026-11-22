using Pos.Application.Authentication;
using Xunit;

namespace Pos.Application.Tests.Authentication;

/// <summary>
/// Pruebas unitarias directas del helper puro <see cref="RefreshTokenMaxAge"/>.
/// Cubren la regla única del Max-Age: mínimo 1 segundo, nunca negativo y
/// redondeo hacia arriba con <see cref="Math.Ceiling(double)"/>.
/// </summary>
public class RefreshTokenMaxAgeHelperTests
{
    private static readonly DateTime s_now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Compute_WhenRemainingIsLessThanOneSecond_ReturnsOne()
    {
        Assert.Equal(1, RefreshTokenMaxAge.Compute(s_now.AddMilliseconds(500), s_now));
    }

    [Fact]
    public void Compute_WhenExpiryEqualsNow_ReturnsOne()
    {
        Assert.Equal(1, RefreshTokenMaxAge.Compute(s_now, s_now));
    }

    [Fact]
    public void Compute_WhenExpiryIsInThePast_ReturnsOne()
    {
        Assert.Equal(1, RefreshTokenMaxAge.Compute(s_now.AddSeconds(-5), s_now));
    }

    [Fact]
    public void Compute_WhenExactlyOneSecond_ReturnsOne()
    {
        Assert.Equal(1, RefreshTokenMaxAge.Compute(s_now.AddSeconds(1), s_now));
    }

    [Fact]
    public void Compute_WhenExactWholeSeconds_ReturnsExactValue()
    {
        Assert.Equal(3600, RefreshTokenMaxAge.Compute(s_now.AddSeconds(3600), s_now));
        Assert.Equal(
            (int)TimeSpan.FromDays(30).TotalSeconds,
            RefreshTokenMaxAge.Compute(s_now.AddDays(30), s_now));
    }

    [Fact]
    public void Compute_WhenFractionalSecond_RoundsUp()
    {
        Assert.Equal(2, RefreshTokenMaxAge.Compute(s_now.AddMilliseconds(1500), s_now));
        Assert.Equal(31, RefreshTokenMaxAge.Compute(s_now.AddMilliseconds(30001), s_now));
    }

    [Fact]
    public void Compute_WhenInstantsHaveNonUtcKind_UsesDifferenceOnly()
    {
        var localNow = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Local);
        var localExpiry = localNow.AddSeconds(90);

        Assert.Equal(90, RefreshTokenMaxAge.Compute(localExpiry, localNow));
    }
}
