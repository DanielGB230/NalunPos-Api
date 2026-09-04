using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class CashRegisterSessionTests
{
    [Fact]
    public void OpenWithValidParametersShouldInstantiateSessionAndEmitEvent()
    {
        // Arrange
        Guid registerId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        var initialAmount = Money.Create(100m, "USD");

        // Act
        var session = CashRegisterSession.Open(registerId, userId, initialAmount, "Apertura inicial");

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
        var session = CashRegisterSession.Open(Guid.NewGuid(), Guid.NewGuid(), Money.Create(100m, "USD"));
        session.Close(Money.Create(150m, "USD"), Money.Create(150m, "USD"));

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            session.Close(Money.Create(150m, "USD"), Money.Create(150m, "USD")));
    }
}
