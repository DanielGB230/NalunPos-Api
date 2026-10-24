using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class CashRegisterSessionTests
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    [Fact]
    public void OpenWithValidParametersShouldInstantiateSessionAndEmitEvent()
    {
        // Arrange
        Guid registerId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        var initialAmount = Money.Create(100m, "USD");

        // Act
        var session = CashRegisterSession.Open(registerId, userId, initialAmount, "Apertura inicial", _timeProvider);

        // Assert
        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Equal(registerId, session.CashRegisterId);
        Assert.Equal(userId, session.UserId);
        Assert.Equal(SessionStatus.Open, session.Status);
        Assert.Equal(initialAmount, session.InitialAmount);
        Assert.Single(session.DomainEvents);
    }

    [Fact]
    public void CloseSessionWhenAlreadyClosedShouldThrowDomainException()
    {
        // Arrange
        var session = CashRegisterSession.Open(Guid.NewGuid(), Guid.NewGuid(), Money.Create(100m, "USD"), _timeProvider);
        session.Close(Money.Create(150m, "USD"), Money.Create(150m, "USD"), _timeProvider);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            session.Close(Money.Create(150m, "USD"), Money.Create(150m, "USD"), _timeProvider));
    }

    [Fact]
    public void OpenAndClose_WithDeterministicTimeProvider_ShouldSetExactTimestamps()
    {
        // Arrange
        var openTime = new DateTimeOffset(2026, 10, 20, 8, 30, 0, TimeSpan.Zero);
        var closeTime = new DateTimeOffset(2026, 10, 20, 17, 30, 0, TimeSpan.Zero);
        var timeProvider = new Common.FakeTimeProvider(openTime);

        Guid registerId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        var initialAmount = Money.Create(100m, "USD");
        var finalAmount = Money.Create(500m, "USD");

        // Act - Open
        var session = CashRegisterSession.Open(registerId, userId, initialAmount, "Apertura con TimeProvider", timeProvider);

        // Assert - Open
        Assert.Equal(openTime.UtcDateTime, session.OpenedAtUtc);
        Assert.Null(session.ClosedAtUtc);

        // Act - Close
        timeProvider.SetUtcNow(closeTime);
        session.Close(finalAmount, finalAmount, "Cierre con TimeProvider", timeProvider);

        // Assert - Close
        Assert.Equal(SessionStatus.Closed, session.Status);
        Assert.Equal(closeTime.UtcDateTime, session.ClosedAtUtc);
    }
}
