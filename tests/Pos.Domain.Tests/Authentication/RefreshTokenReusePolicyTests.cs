using Pos.Domain.Authentication;
using Pos.Domain.Exceptions;
using Xunit;

namespace Pos.Domain.Tests.Authentication;

/// <summary>
/// Política pura de clasificación de un refresh token revocado (F1.G.5):
/// distingue una rotación concurrente dentro de la ventana de tolerancia
/// de un reuso real que debe revocar la familia.
/// </summary>
public class RefreshTokenReusePolicyTests
{
    private static readonly DateTime s_nowUtc = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan s_leeway = TimeSpan.FromSeconds(10);

    [Fact]
    public void Classify_WithRotatedReasonInsideWindow_ShouldReturnConcurrentRotation()
    {
        DateTime revokedAtUtc = s_nowUtc.AddSeconds(-5);

        var classification = RefreshTokenReusePolicy.Classify(
            RefreshTokenRevocationReasons.Rotated,
            revokedAtUtc,
            s_nowUtc,
            s_leeway);

        Assert.Equal(RefreshTokenReuseClassification.ConcurrentRotation, classification);
    }

    [Fact]
    public void Classify_WithRotatedReasonExactlyAtWindowLimit_ShouldReturnConcurrentRotation()
    {
        DateTime revokedAtUtc = s_nowUtc.Subtract(s_leeway);

        var classification = RefreshTokenReusePolicy.Classify(
            RefreshTokenRevocationReasons.Rotated,
            revokedAtUtc,
            s_nowUtc,
            s_leeway);

        Assert.Equal(RefreshTokenReuseClassification.ConcurrentRotation, classification);
    }

    [Fact]
    public void Classify_WithRotatedReasonOneTickOutsideWindow_ShouldReturnReuse()
    {
        DateTime revokedAtUtc = s_nowUtc.Subtract(s_leeway).AddTicks(-1);

        var classification = RefreshTokenReusePolicy.Classify(
            RefreshTokenRevocationReasons.Rotated,
            revokedAtUtc,
            s_nowUtc,
            s_leeway);

        Assert.Equal(RefreshTokenReuseClassification.Reuse, classification);
    }

    [Theory]
    [InlineData(RefreshTokenRevocationReasons.Logout)]
    [InlineData(RefreshTokenRevocationReasons.ReuseDetected)]
    [InlineData(RefreshTokenRevocationReasons.Expired)]
    public void Classify_WithNonRotatedReasonInsideWindow_ShouldAlwaysReturnReuse(string reason)
    {
        var classification = RefreshTokenReusePolicy.Classify(
            reason,
            s_nowUtc.AddSeconds(-1),
            s_nowUtc,
            s_leeway);

        Assert.Equal(RefreshTokenReuseClassification.Reuse, classification);
    }

    [Fact]
    public void Classify_WithZeroLeeway_ShouldReturnReuseEvenForImmediateRotation()
    {
        var classification = RefreshTokenReusePolicy.Classify(
            RefreshTokenRevocationReasons.Rotated,
            s_nowUtc,
            s_nowUtc,
            TimeSpan.Zero);

        Assert.Equal(RefreshTokenReuseClassification.Reuse, classification);
    }

    [Fact]
    public void Classify_WithNegativeElapsedDueToClockSkew_ShouldReturnConcurrentRotation()
    {
        DateTime revokedAtUtc = s_nowUtc.AddSeconds(2);

        var classification = RefreshTokenReusePolicy.Classify(
            RefreshTokenRevocationReasons.Rotated,
            revokedAtUtc,
            s_nowUtc,
            s_leeway);

        Assert.Equal(RefreshTokenReuseClassification.ConcurrentRotation, classification);
    }

    [Fact]
    public void Classify_WithNullRevokedAtUtc_ShouldReturnReuse()
    {
        var classification = RefreshTokenReusePolicy.Classify(
            RefreshTokenRevocationReasons.Rotated,
            revokedAtUtc: null,
            s_nowUtc,
            s_leeway);

        Assert.Equal(RefreshTokenReuseClassification.Reuse, classification);
    }

    [Fact]
    public void Classify_WithNonUtcNow_ShouldThrowDomainException()
    {
        DateTime localNow = DateTime.SpecifyKind(s_nowUtc, DateTimeKind.Local);

        Assert.Throws<DomainException>(() => RefreshTokenReusePolicy.Classify(
            RefreshTokenRevocationReasons.Rotated,
            s_nowUtc.AddSeconds(-1),
            localNow,
            s_leeway));
    }

    [Fact]
    public void Classify_WithNonUtcRevokedAtUtc_ShouldThrowDomainException()
    {
        DateTime localRevokedAt = DateTime.SpecifyKind(s_nowUtc.AddSeconds(-1), DateTimeKind.Local);

        Assert.Throws<DomainException>(() => RefreshTokenReusePolicy.Classify(
            RefreshTokenRevocationReasons.Rotated,
            localRevokedAt,
            s_nowUtc,
            s_leeway));
    }
}
