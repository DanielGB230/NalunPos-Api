using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class InvoiceTests
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    [Fact]
    public void CreateInvoiceShouldEmitInvoiceIssuedDomainEvent()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        Guid saleId = Guid.NewGuid();
        var taxId = TaxId.Create("20123456789", "PE");
        var amount = Money.Create(295m, "USD");

        // Act
        var invoice = Invoice.Create(tenantId, saleId, InvoiceDocumentType.Invoice, "F001-00001", taxId, amount, _timeProvider);

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
            Invoice.Create(PlatformScope.TenantId, saleId, InvoiceDocumentType.Invoice, "F001-00001", taxId, amount, _timeProvider));

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
            Invoice.Create(Guid.Empty, saleId, InvoiceDocumentType.Invoice, "F001-00001", taxId, amount, _timeProvider));

        Assert.Equal("El ID del tenant es requerido para crear una factura.", ex.Message);
    }

    [Fact]
    public void CreateAndMarkAsSent_WithDeterministicTimeProvider_ShouldSetExactTimestampsAndEmitEvents()
    {
        // Arrange
        var issueTime = new DateTimeOffset(2026, 10, 20, 10, 30, 0, TimeSpan.Zero);
        var sentTime = new DateTimeOffset(2026, 10, 20, 10, 45, 0, TimeSpan.Zero);
        var timeProvider = new Common.FakeTimeProvider(issueTime);

        var tenantId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var taxId = TaxId.Create("20123456789", "PE");
        var amount = Money.Create(295m, "USD");

        // Act - Create
        var invoice = Invoice.Create(tenantId, saleId, InvoiceDocumentType.Invoice, "F001-00002", taxId, amount, timeProvider);

        // Assert - Create
        Assert.Equal(issueTime.UtcDateTime, invoice.IssueDateUtc);
        var issuedEvent = Assert.IsType<Pos.Domain.DomainEvents.InvoiceIssuedDomainEvent>(Assert.Single(invoice.DomainEvents));
        Assert.Equal(issueTime.UtcDateTime, issuedEvent.OccurredOnUtc);

        // Act - MarkAsSent
        timeProvider.SetUtcNow(sentTime);
        invoice.MarkAsSent(timeProvider);

        // Assert - MarkAsSent
        Assert.Equal(InvoiceStatus.Sent, invoice.Status);
        var statusEvent = invoice.DomainEvents.OfType<Pos.Domain.DomainEvents.InvoiceStatusUpdatedDomainEvent>().Single();
        Assert.Equal(sentTime.UtcDateTime, statusEvent.OccurredOnUtc);
    }

    [Fact]
    public void Invoice_WhenCreated_HasNullManualInterventionFields()
    {
        var tenantId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var taxId = TaxId.Create("20123456789");
        var amount = Money.Create(100m);

        var invoice = Invoice.Create(tenantId, saleId, InvoiceDocumentType.Invoice, "F001-00003", taxId, amount, _timeProvider);

        Assert.Null(invoice.ManualInterventionReason);
        Assert.Null(invoice.ManualInterventionAtUtc);
    }

    [Fact]
    public void MarkAsRequiresManualIntervention_SetsReasonAndDate_AndValidatesNonNullAndNonEmpty()
    {
        var tenantId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var taxId = TaxId.Create("20123456789");
        var amount = Money.Create(100m);
        var interventionTime = new DateTimeOffset(2026, 10, 27, 15, 30, 0, TimeSpan.Zero);
        var timeProvider = new Common.FakeTimeProvider(interventionTime);

        var invoice = Invoice.Create(tenantId, saleId, InvoiceDocumentType.Invoice, "F001-00004", taxId, amount, timeProvider);

        const string reason = "Proveedor SUNAT no disponible tras reintentos agotados.";
        invoice.MarkAsRequiresManualIntervention(reason, timeProvider);

        Assert.Equal(InvoiceStatus.RequiresManualIntervention, invoice.Status);
        Assert.Equal(reason, invoice.ManualInterventionReason);
        Assert.Equal(interventionTime.UtcDateTime, invoice.ManualInterventionAtUtc);

        var exhaustedEvent = Assert.Single(invoice.DomainEvents.OfType<Pos.Domain.DomainEvents.InvoiceReconciliationExhaustedDomainEvent>());
        Assert.Equal(reason, exhaustedEvent.Reason);
        Assert.Equal(interventionTime.UtcDateTime, exhaustedEvent.OccurredOnUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MarkAsRequiresManualIntervention_WhenReasonEmptyOrNull_ThrowsDomainException(string? invalidReason)
    {
        var tenantId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var taxId = TaxId.Create("20123456789");
        var amount = Money.Create(100m);
        var invoice = Invoice.Create(tenantId, saleId, InvoiceDocumentType.Invoice, "F001-00005", taxId, amount, _timeProvider);

        Assert.Throws<Pos.Domain.Exceptions.DomainException>(() =>
            invoice.MarkAsRequiresManualIntervention(invalidReason!, _timeProvider));
    }

    [Fact]
    public void Invoice_Invariant_ManualInterventionFields_NullWhenNotInRequiresManualIntervention_AndPresentWhenInStatus()
    {
        var tenantId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var taxId = TaxId.Create("20123456789");
        var amount = Money.Create(100m);
        var invoice = Invoice.Create(tenantId, saleId, InvoiceDocumentType.Invoice, "F001-00006", taxId, amount, _timeProvider);

        // Estado inicial Pending: ambos nulos
        Assert.Null(invoice.ManualInterventionReason);
        Assert.Null(invoice.ManualInterventionAtUtc);

        // Transición a RequiresManualIntervention: ambos presentes
        invoice.MarkAsRequiresManualIntervention("Motivo de prueba", _timeProvider);
        Assert.NotNull(invoice.ManualInterventionReason);
        Assert.NotNull(invoice.ManualInterventionAtUtc);

        // Transición posterior fuera de ese estado (ej. Accepted): ambos nulos
        invoice.MarkAsAccepted(_timeProvider);
        Assert.Null(invoice.ManualInterventionReason);
        Assert.Null(invoice.ManualInterventionAtUtc);
    }
}
