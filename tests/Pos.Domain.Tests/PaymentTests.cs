using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class PaymentTests
{
    [Fact]
    public void CreatePaymentShouldInstantiatePendingPaymentWithNoSensitiveCardData()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        Guid saleId = Guid.NewGuid();
        var amount = Money.Create(295m, "USD");

        // Act
        var payment = Payment.Create(tenantId, saleId, amount, PaymentMethod.CreditCard, "VOUCHER-9988");

        // Assert
        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(tenantId, payment.TenantId);
        Assert.Equal(saleId, payment.SaleId);
        Assert.Equal(amount, payment.Amount);
        Assert.Equal(PaymentMethod.CreditCard, payment.Method);
        Assert.Equal("VOUCHER-9988", payment.ExternalReference);
        Assert.Equal(PaymentStatus.Pending, payment.Status);

        // Verificación PCI-DSS Sec 12: La clase Payment no posee propiedades para números de tarjeta ni CVV
        var properties = typeof(Payment).GetProperties();
        Assert.DoesNotContain(properties, p => p.Name.Contains("Pan", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, p => p.Name.Contains("CardNumber", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, p => p.Name.Contains("Cvv", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProcessPaymentShouldChangeStatusToProcessedAndEmitEvent()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var payment = Payment.Create(tenantId, Guid.NewGuid(), Money.Create(100m, "USD"), PaymentMethod.Cash);

        // Act
        payment.Process("CASH-REF-001");

        // Assert
        Assert.Equal(PaymentStatus.Processed, payment.Status);
        Assert.Single(payment.DomainEvents);
        Assert.Equal("PaymentProcessedDomainEvent", payment.DomainEvents.First().GetType().Name);
    }

    [Fact]
    public void CreatePaymentWithPlatformScopeTenantIdShouldThrowDomainException()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var amount = Money.Create(100m, "USD");

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Payment.Create(PlatformScope.TenantId, saleId, amount, PaymentMethod.Cash));

        Assert.Equal("El ID del tenant es requerido para crear un pago.", ex.Message);
    }

    [Fact]
    public void CreatePaymentWithEmptyTenantIdShouldThrowDomainException()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var amount = Money.Create(100m, "USD");

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            Payment.Create(Guid.Empty, saleId, amount, PaymentMethod.Cash));

        Assert.Equal("El ID del tenant es requerido para crear un pago.", ex.Message);
    }
}
