using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class SaleTests
{
    [Fact]
    public void CreateSaleWithValidItemsShouldCalculateTotalsAndEmitSaleCompletedDomainEvent()
    {
        // Arrange
        string receiptNumber = "REC-2026-0001";
        Guid sessionId = Guid.NewGuid();
        Guid productId1 = Guid.NewGuid();
        Guid productId2 = Guid.NewGuid();

        var item1 = SaleLineItem.Create(productId1, "Teclado Mecánico", 2m, Money.Create(100m, "USD"));
        var item2 = SaleLineItem.Create(productId2, "Mouse Gamer", 1m, Money.Create(50m, "USD"));
        var items = new List<SaleLineItem> { item1, item2 };

        // Act
        var sale = Sale.Create(receiptNumber, sessionId, null, items, taxRatePercentage: 18m, currency: "USD");

        // Assert
        Assert.NotEqual(Guid.Empty, sale.Id);
        Assert.Equal(receiptNumber, sale.ReceiptNumber);
        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Equal(250m, sale.SubTotal.Amount); // (2*100) + (1*50) = 250
        Assert.Equal(45m, sale.TaxAmount.Amount); // 250 * 0.18 = 45
        Assert.Equal(295m, sale.Total.Amount); // 250 + 45 = 295
        Assert.Equal(2, sale.LineItems.Count);

        Assert.Single(sale.DomainEvents);
        var domainEvent = sale.DomainEvents.First();
        Assert.Equal("SaleCompletedDomainEvent", domainEvent.GetType().Name);
    }

    [Fact]
    public void CreateSaleWithNoItemsShouldThrowDomainException()
    {
        // Arrange & Act & Assert
        Assert.Throws<DomainException>(() =>
            Sale.Create("REC-001", Guid.NewGuid(), null, new List<SaleLineItem>()));
    }
}
