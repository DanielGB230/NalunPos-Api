using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class InvoiceTests
{
    [Fact]
    public void CreateInvoiceShouldEmitInvoiceIssuedDomainEvent()
    {
        // Arrange
        Guid saleId = Guid.NewGuid();
        var taxId = TaxId.Create("20123456789", "PE");
        var amount = Money.Create(295m, "USD");

        // Act
        var invoice = Invoice.Create(saleId, InvoiceDocumentType.Invoice, "F001-00001", taxId, amount);

        // Assert
        Assert.NotEqual(Guid.Empty, invoice.Id);
        Assert.Equal(saleId, invoice.SaleId);
        Assert.Equal("F001-00001", invoice.DocumentNumber);
        Assert.Equal(InvoiceStatus.Pending, invoice.Status);
        Assert.Single(invoice.DomainEvents);
        Assert.Equal("InvoiceIssuedDomainEvent", invoice.DomainEvents.First().GetType().Name);
    }
}
