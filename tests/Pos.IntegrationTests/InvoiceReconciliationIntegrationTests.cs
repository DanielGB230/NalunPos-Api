using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pos.Api.BackgroundServices;
using Pos.Api.Options;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.ValueObjects;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

[Collection("IntegrationTests")]
public class InvoiceReconciliationIntegrationTests
{
    private readonly MsSqlTestFixture _fixture;

    public InvoiceReconciliationIntegrationTests(MsSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task InvoiceReconciliation_DiscoversCrossTenantInvoices_UnderRls_AndReconcilesInSeparateScopes()
    {
        // 1. Arrange: 2 Tenants con facturas Pending vencidas y límites de reintentos
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        var saleId1 = Guid.NewGuid();
        var saleId2 = Guid.NewGuid();

        var now = DateTime.UtcNow;
        var tenMinutesAgo = now.AddMinutes(-10);
        var fifteenMinutesAgo = now.AddMinutes(-15);
        var twoMinutesAgo = now.AddMinutes(-2);

        // Sembrar en Tenant 1
        using (var db1 = _fixture.CreateDbContext(tenant1Id))
        {
            var invoice1A = Invoice.Create(
                tenant1Id,
                saleId1,
                InvoiceDocumentType.Invoice,
                $"F001-{Guid.NewGuid().ToString()[..6]}",
                TaxId.Create("20123456789"),
                Money.Create(100m));
            SetPrivateProperty(invoice1A, nameof(Invoice.IssueDateUtc), tenMinutesAgo);

            var invoice1B = Invoice.Create(
                tenant1Id,
                saleId1,
                InvoiceDocumentType.Invoice,
                $"F001-{Guid.NewGuid().ToString()[..6]}",
                TaxId.Create("20123456789"),
                Money.Create(200m));
            SetPrivateProperty(invoice1B, nameof(Invoice.IssueDateUtc), fifteenMinutesAgo);
            SetPrivateProperty(invoice1B, nameof(Invoice.ReconciliationAttempts), 5); // Max attempts alcanzado

            db1.Invoices.AddRange(invoice1A, invoice1B);
            await db1.SaveChangesAsync();
        }

        // Sembrar en Tenant 2
        using (var db2 = _fixture.CreateDbContext(tenant2Id))
        {
            var invoice2A = Invoice.Create(
                tenant2Id,
                saleId2,
                InvoiceDocumentType.Receipt,
                $"B001-{Guid.NewGuid().ToString()[..6]}",
                TaxId.Create("10123456789"),
                Money.Create(50m));
            SetPrivateProperty(invoice2A, nameof(Invoice.IssueDateUtc), tenMinutesAgo);

            var invoice2B = Invoice.Create(
                tenant2Id,
                saleId2,
                InvoiceDocumentType.Receipt,
                $"B001-{Guid.NewGuid().ToString()[..6]}",
                TaxId.Create("10123456789"),
                Money.Create(75m));
            SetPrivateProperty(invoice2B, nameof(Invoice.IssueDateUtc), twoMinutesAgo); // No vencida aún (< 5m)

            db2.Invoices.AddRange(invoice2A, invoice2B);
            await db2.SaveChangesAsync();
        }

        // 2. Configurar el Worker con ServiceProvider de infraestructura
        var sp = _fixture.CreateServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var logger = sp.GetRequiredService<ILogger<InvoiceReconciliationBackgroundService>>();

        var settings = Options.Create(new InvoiceReconciliationSettings
        {
            IntervalSeconds = 5,
            ThresholdMinutes = 5,
            MaxReconciliationAttempts = 5,
            BatchSize = 10
        });

        var worker = new InvoiceReconciliationBackgroundService(
            scopeFactory,
            logger,
            settings,
            TimeProvider.System);

        // 3. Act: Ejecutar ciclo de reconciliación
        await worker.ReconcilePendingInvoicesAsync(CancellationToken.None);

        // 4. Assert: Verificar bajo RLS en cada tenant
        using (var verifyDb1 = _fixture.CreateDbContext(tenant1Id))
        {
            var invoicesTenant1 = await verifyDb1.Invoices.ToListAsync();

            // Factura 1A vencida fue descubierta por el lector de plataforma y reconciliada vía ReconcileInvoiceCommand
            var inv1A = invoicesTenant1.Single(i => i.TotalAmount.Amount == 100m);
            Assert.Equal(InvoiceStatus.Accepted, inv1A.Status);

            // Factura 1B superó MaxReconciliationAttempts=5, no fue reconciliada
            var inv1B = invoicesTenant1.Single(i => i.TotalAmount.Amount == 200m);
            Assert.Equal(InvoiceStatus.Pending, inv1B.Status);
            Assert.Equal(5, inv1B.ReconciliationAttempts);

            // Aislamiento RLS: Tenant 1 nunca ve facturas de Tenant 2
            Assert.DoesNotContain(invoicesTenant1, i => i.TenantId == tenant2Id);
        }

        using (var verifyDb2 = _fixture.CreateDbContext(tenant2Id))
        {
            var invoicesTenant2 = await verifyDb2.Invoices.ToListAsync();

            // Factura 2A vencida fue descubierta y reconciliada
            var inv2A = invoicesTenant2.Single(i => i.TotalAmount.Amount == 50m);
            Assert.Equal(InvoiceStatus.Accepted, inv2A.Status);

            // Factura 2B no estaba vencida (hace 2 min), sigue Pending
            var inv2B = invoicesTenant2.Single(i => i.TotalAmount.Amount == 75m);
            Assert.Equal(InvoiceStatus.Pending, inv2B.Status);

            // Aislamiento RLS: Tenant 2 nunca ve facturas de Tenant 1
            Assert.DoesNotContain(invoicesTenant2, i => i.TenantId == tenant1Id);
        }
    }

    [Fact]
    public async Task InvoiceReconciliation_BatchSize_LimitsProcessedInvoices()
    {
        var tenantId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var tenMinutesAgo = DateTime.UtcNow.AddMinutes(-10);

        using (var db = _fixture.CreateDbContext(tenantId))
        {
            for (int i = 0; i < 3; i++)
            {
                var invoice = Invoice.Create(
                    tenantId,
                    saleId,
                    InvoiceDocumentType.Invoice,
                    $"F002-{Guid.NewGuid().ToString()[..6]}",
                    TaxId.Create("20123456789"),
                    Money.Create(10m * (i + 1)));
                SetPrivateProperty(invoice, nameof(Invoice.IssueDateUtc), tenMinutesAgo);
                db.Invoices.Add(invoice);
            }
            await db.SaveChangesAsync();
        }

        var sp = _fixture.CreateServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var logger = sp.GetRequiredService<ILogger<InvoiceReconciliationBackgroundService>>();

        // BatchSize = 1: solo debe procesar 1 en este ciclo
        var settings = Options.Create(new InvoiceReconciliationSettings
        {
            IntervalSeconds = 5,
            ThresholdMinutes = 5,
            MaxReconciliationAttempts = 5,
            BatchSize = 1
        });

        var worker = new InvoiceReconciliationBackgroundService(
            scopeFactory,
            logger,
            settings,
            TimeProvider.System);

        await worker.ReconcilePendingInvoicesAsync(CancellationToken.None);

        using (var verifyDb = _fixture.CreateDbContext(tenantId))
        {
            var acceptedCount = await verifyDb.Invoices.CountAsync(i => i.Status == InvoiceStatus.Accepted);
            Assert.Equal(1, acceptedCount);
        }
    }

    private static void SetPrivateProperty<T>(T obj, string propertyName, object value)
    {
        var prop = typeof(T).GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(obj, value);
    }
}
