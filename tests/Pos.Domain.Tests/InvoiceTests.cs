using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class InvoiceTests
{
    [Fact]
    public void CreateInvoiceShouldEmitInvoiceIssuedDomainEvent()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        Guid saleId = Guid.NewGuid();
        var taxId = TaxId.Create("20123456789", "PE");
        var amount = Money.Create(295m, "USD");

        // Act
        var invoice = Invoice.Create(tenantId, saleId, InvoiceDocumentType.Invoice, "F001-00001", taxId, amount);

        // Assert
        Assert.NotEqual(Guid.Empty, invoice.Id);
        Assert.Equal(tenantId, invoice.TenantId);
        Assert.Equal(saleId, invoice.SaleId);
        Assert.Equal("F001-00001", invoice.DocumentNumber);
        Assert.Equal(InvoiceStatus.Pending, invoice.Status);
        Assert.Single(invoice.DomainEvents);
        Assert.Equal("InvoiceIssuedDomainEvent", invoice.DomainEvents.First().GetType().Name);
    }

    [Fact]
    public void CreateInvoiceWithPlatformScopeTenantIdShouldThrowDomainException()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var taxId = TaxId.Create("20123456789", "PE");
        var amount = Money.Create(295m, "USD");

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Invoice.Create(PlatformScope.TenantId, saleId, InvoiceDocumentType.Invoice, "F001-00001", taxId, amount));

        Assert.Equal("El ID del tenant es requerido para crear una factura.", ex.Message);
    }

    [Fact]
    public void CreateInvoiceWithEmptyTenantIdShouldThrowDomainException()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var taxId = TaxId.Create("20123456789", "PE");
        var amount = Money.Create(295m, "USD");

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Invoice.Create(Guid.Empty, saleId, InvoiceDocumentType.Invoice, "F001-00001", taxId, amount));

        Assert.Equal("El ID del tenant es requerido para crear una factura.", ex.Message);
    }
}
