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
                Money.Create(100m),
                TimeProvider.System);
            SetPrivateProperty(invoice1A, nameof(Invoice.IssueDateUtc), tenMinutesAgo);

            var invoice1B = Invoice.Create(
                tenant1Id,
                saleId1,
                InvoiceDocumentType.Invoice,
                $"F001-{Guid.NewGuid().ToString()[..6]}",
                TaxId.Create("20123456789"),
                Money.Create(200m),
                TimeProvider.System);
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
                Money.Create(50m),
                TimeProvider.System);
            SetPrivateProperty(invoice2A, nameof(Invoice.IssueDateUtc), tenMinutesAgo);

            var invoice2B = Invoice.Create(
                tenant2Id,
                saleId2,
                InvoiceDocumentType.Receipt,
                $"B001-{Guid.NewGuid().ToString()[..6]}",
                TaxId.Create("10123456789"),
                Money.Create(75m),
                TimeProvider.System);
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
                    Money.Create(10m * (i + 1)),
                    TimeProvider.System);
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

    [Fact]
    public async Task InvoiceReconciliation_WhenExhaustedInvoicesExist_ProcessesDueInvoicesInSameCycleAndNeverResendsExhausted()
    {
        // 1. Arrange: 60 facturas agotadas más antiguas (30 min) y 3 nuevas vencidas (10 min)
        var tenantId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var thirtyMinutesAgo = DateTime.UtcNow.AddMinutes(-30);
        var tenMinutesAgo = DateTime.UtcNow.AddMinutes(-10);

        var exhaustedInvoiceIds = new List<Guid>();
        var newDueInvoiceIds = new List<Guid>();

        using (var db = _fixture.CreateDbContext(tenantId))
        {
            // 60 facturas agotadas
            for (int i = 0; i < 60; i++)
            {
                var exhaustedInv = Invoice.Create(
                    tenantId,
                    saleId,
                    InvoiceDocumentType.Invoice,
                    $"F060-{Guid.NewGuid().ToString()[..6]}",
                    TaxId.Create("20123456789"),
                    Money.Create(15m + i),
                    TimeProvider.System);
                SetPrivateProperty(exhaustedInv, nameof(Invoice.IssueDateUtc), thirtyMinutesAgo);
                SetPrivateProperty(exhaustedInv, nameof(Invoice.ReconciliationAttempts), 5); // Agotada
                db.Invoices.Add(exhaustedInv);
                exhaustedInvoiceIds.Add(exhaustedInv.Id);
            }

            // 3 facturas nuevas vencidas
            for (int i = 0; i < 3; i++)
            {
                var dueInv = Invoice.Create(
                    tenantId,
                    saleId,
                    InvoiceDocumentType.Invoice,
                    $"F003-{Guid.NewGuid().ToString()[..6]}",
                    TaxId.Create("20123456789"),
                    Money.Create(100m + i),
                    TimeProvider.System);
                SetPrivateProperty(dueInv, nameof(Invoice.IssueDateUtc), tenMinutesAgo);
                SetPrivateProperty(dueInv, nameof(Invoice.ReconciliationAttempts), 0); // Nueva
                db.Invoices.Add(dueInv);
                newDueInvoiceIds.Add(dueInv.Id);
            }

            await db.SaveChangesAsync();
        }

        // Registrar Spy para vigilar que el proveedor tributario nunca reciba facturas agotadas
        var spy = new SpyElectronicInvoicingService(new Pos.Infrastructure.ExternalServices.Dummy.DummyElectronicInvoicingService());
        var sp = _fixture.CreateServiceProvider(
            tenantId: tenantId,
            configureServices: services =>
            {
                services.AddScoped<Pos.Application.Common.Interfaces.IElectronicInvoicingService>(_ => spy);
            });

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

        // 2. Act: Ejecutar el ciclo de reconciliación
        await worker.ReconcilePendingInvoicesAsync(CancellationToken.None);

        // 3. Assert:
        // (a) Las 3 nuevas vencidas fueron procesadas en este mismo ciclo a pesar de las 60 más antiguas
        using (var verifyDb = _fixture.CreateDbContext(tenantId))
        {
            var processedInvoices = await verifyDb.Invoices
                .Where(i => newDueInvoiceIds.Contains(i.Id))
                .ToListAsync();

            Assert.Equal(3, processedInvoices.Count);
            Assert.All(processedInvoices, inv => Assert.Equal(InvoiceStatus.Accepted, inv.Status));

            // (b) Las 60 agotadas nunca fueron enviadas al proveedor
            foreach (var exhaustedId in exhaustedInvoiceIds)
            {
                Assert.DoesNotContain(exhaustedId, spy.InvoicesSent);
            }

            // Solo los 3 IDs de las nuevas vencidas llegaron al proveedor
            Assert.Equal(3, spy.InvoicesSent.Count);
            foreach (var dueId in newDueInvoiceIds)
            {
                Assert.Contains(dueId, spy.InvoicesSent);
            }
        }

        // (c) Si intentamos ejecutar directamente ReconcileInvoiceCommand sobre una factura agotada,
        // el caso de uso transiciona a RequiresManualIntervention de forma controlada (Result.Ok con Status RequiresManualIntervention)
        // y sin tocar el proveedor
        using (var scope = scopeFactory.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Pos.Application.Common.Interfaces.IDispatcher>();
            var targetExhaustedId = exhaustedInvoiceIds.First();

            var directResult = await dispatcher.SendAsync(new Pos.Application.Invoicing.Commands.ReconcileInvoiceCommand(targetExhaustedId, MaxAttempts: 5));

            Assert.True(directResult.IsSuccess);
            Assert.Equal(InvoiceStatus.RequiresManualIntervention.ToString(), directResult.Value.StatusName);
            // El proveedor no recibió ninguna llamada adicional
            Assert.DoesNotContain(targetExhaustedId, spy.InvoicesSent);

            using var verifyDb = _fixture.CreateDbContext(tenantId);
            var updatedExhausted = await verifyDb.Invoices.FindAsync(targetExhaustedId);
            Assert.NotNull(updatedExhausted);
            Assert.Equal(InvoiceStatus.RequiresManualIntervention, updatedExhausted.Status);
        }
    }

    private sealed class SpyElectronicInvoicingService : Pos.Application.Common.Interfaces.IElectronicInvoicingService
    {
        private readonly Pos.Application.Common.Interfaces.IElectronicInvoicingService _inner;
        public System.Collections.Concurrent.ConcurrentBag<Guid> InvoicesSent { get; } = new();

        public SpyElectronicInvoicingService(Pos.Application.Common.Interfaces.IElectronicInvoicingService inner)
        {
            _inner = inner;
        }

        public Task<Pos.Application.Common.Interfaces.InvoicingServiceResult> SendInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(invoice);
            InvoicesSent.Add(invoice.Id);
            return _inner.SendInvoiceAsync(invoice, cancellationToken);
        }

        public Task<Pos.Application.Common.Interfaces.InvoicingServiceResult> GetStatusAsync(Invoice invoice, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(invoice);
            InvoicesSent.Add(invoice.Id);
            return _inner.GetStatusAsync(invoice, cancellationToken);
        }
    }

    private static void SetPrivateProperty<T>(T obj, string propertyName, object value)
    {
        var prop = typeof(T).GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(obj, value);
    }

    [Fact]
    public async Task ExhaustedPendingInvoices_WhenMigratedAcrossTwoTenants_TransitionsToRequiresManualInterventionAndGeneratesOutboxAlertsPerTenantWithoutCrossContamination()
    {
        // 1. Arrange: 2 Tenants con facturas Pending agotadas y no agotadas
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();
        var saleId1 = Guid.NewGuid();
        var saleId2 = Guid.NewGuid();

        Guid exhausted1Id;
        Guid pending1Id;
        Guid exhausted2Id;
        Guid pending2Id;

        // Sembrar en Tenant 1
        using (var db1 = _fixture.CreateDbContext(tenant1Id))
        {
            var inv1Exhausted = Invoice.Create(
                tenant1Id,
                saleId1,
                InvoiceDocumentType.Invoice,
                $"F001-{Guid.NewGuid():N}"[..12],
                TaxId.Create("20123456789"),
                Money.Create(100m),
                TimeProvider.System);
            SetPrivateProperty(inv1Exhausted, nameof(Invoice.ReconciliationAttempts), 5); // Agotada

            var inv1Pending = Invoice.Create(
                tenant1Id,
                saleId1,
                InvoiceDocumentType.Invoice,
                $"F001-{Guid.NewGuid():N}"[..12],
                TaxId.Create("20123456789"),
                Money.Create(150m),
                TimeProvider.System);
            SetPrivateProperty(inv1Pending, nameof(Invoice.ReconciliationAttempts), 1); // No agotada

            db1.Invoices.AddRange(inv1Exhausted, inv1Pending);
            await db1.SaveChangesAsync();

            exhausted1Id = inv1Exhausted.Id;
            pending1Id = inv1Pending.Id;
        }

        // Sembrar en Tenant 2
        using (var db2 = _fixture.CreateDbContext(tenant2Id))
        {
            var inv2Exhausted = Invoice.Create(
                tenant2Id,
                saleId2,
                InvoiceDocumentType.Receipt,
                $"B001-{Guid.NewGuid():N}"[..12],
                TaxId.Create("10123456789"),
                Money.Create(200m),
                TimeProvider.System);
            SetPrivateProperty(inv2Exhausted, nameof(Invoice.ReconciliationAttempts), 7); // Agotada (>5)

            var inv2Pending = Invoice.Create(
                tenant2Id,
                saleId2,
                InvoiceDocumentType.Receipt,
                $"B001-{Guid.NewGuid():N}"[..12],
                TaxId.Create("10123456789"),
                Money.Create(250m),
                TimeProvider.System);
            SetPrivateProperty(inv2Pending, nameof(Invoice.ReconciliationAttempts), 2); // No agotada

            db2.Invoices.AddRange(inv2Exhausted, inv2Pending);
            await db2.SaveChangesAsync();

            exhausted2Id = inv2Exhausted.Id;
            pending2Id = inv2Pending.Id;
        }

        // 2. Act: Ejecutar la lógica de migración con contexto SuperAdmin en SQL Server
        using (var adminDb = _fixture.CreateDbContext(null, isSuperAdmin: true))
        {
            await adminDb.Database.ExecuteSqlRawAsync(@"
EXEC sys.sp_set_session_context @key = N'IsSuperAdmin', @value = 1;

INSERT INTO [dbo].[OutboxMessages] (
    [Id],
    [TenantId],
    [Type],
    [Content],
    [OccurredOnUtc],
    [ProcessedOnUtc],
    [RetryCount],
    [IsDeadLetter]
)
SELECT
    NEWID() AS [Id],
    i.[TenantId],
    'Pos.Domain.DomainEvents.InvoiceReconciliationExhaustedDomainEvent' AS [Type],
    CONCAT(
        '{{\""InvoiceId\"":\""', CAST(i.[Id] AS NVARCHAR(36)),
        '\"",\""SaleId\"":\""', CAST(i.[SaleId] AS NVARCHAR(36)),
        '\"",\""TenantId\"":\""', CAST(i.[TenantId] AS NVARCHAR(36)),
        '\"",\""DocumentNumber\"":\""', i.[DocumentNumber],
        '\"",\""Attempts\"":', CAST(i.[ReconciliationAttempts] AS NVARCHAR(10)),
        ',\""Reason\"":\""Superó el límite de reintentos de reconciliación fiscal sin confirmación del proveedor.\"",',
        '\""OccurredOnUtc\"":\""', CONVERT(NVARCHAR(30), SYSUTCDATETIME(), 127),
        'Z\""}}'
    ) AS [Content],
    SYSUTCDATETIME() AS [OccurredOnUtc],
    NULL AS [ProcessedOnUtc],
    0 AS [RetryCount],
    0 AS [IsDeadLetter]
FROM [dbo].[Invoices] i
WHERE i.[Status] = 1 AND i.[ReconciliationAttempts] >= 5;

UPDATE [dbo].[Invoices]
SET [Status] = 5
WHERE [Status] = 1 AND [ReconciliationAttempts] >= 5;
");
        }

        // 3. Assert Tenant 1:
        using (var verifyDb1 = _fixture.CreateDbContext(tenant1Id))
        {
            var inv1A = await verifyDb1.Invoices.FindAsync(exhausted1Id);
            var inv1B = await verifyDb1.Invoices.FindAsync(pending1Id);

            Assert.NotNull(inv1A);
            Assert.Equal(InvoiceStatus.RequiresManualIntervention, inv1A.Status);

            Assert.NotNull(inv1B);
            Assert.Equal(InvoiceStatus.Pending, inv1B.Status);

            // Outbox en Tenant 1 bajo RLS: solo ve sus propias alertas
            var alerts1 = await verifyDb1.OutboxMessages
                .Where(o => o.Type == "Pos.Domain.DomainEvents.InvoiceReconciliationExhaustedDomainEvent")
                .ToListAsync();

            Assert.Contains(alerts1, a => a.TenantId == tenant1Id && a.Content.Contains(exhausted1Id.ToString(), StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(alerts1, a => a.TenantId == tenant2Id);
        }

        // 4. Assert Tenant 2:
        using (var verifyDb2 = _fixture.CreateDbContext(tenant2Id))
        {
            var inv2A = await verifyDb2.Invoices.FindAsync(exhausted2Id);
            var inv2B = await verifyDb2.Invoices.FindAsync(pending2Id);

            Assert.NotNull(inv2A);
            Assert.Equal(InvoiceStatus.RequiresManualIntervention, inv2A.Status);

            Assert.NotNull(inv2B);
            Assert.Equal(InvoiceStatus.Pending, inv2B.Status);

            // Outbox en Tenant 2 bajo RLS: solo ve sus propias alertas
            var alerts2 = await verifyDb2.OutboxMessages
                .Where(o => o.Type == "Pos.Domain.DomainEvents.InvoiceReconciliationExhaustedDomainEvent")
                .ToListAsync();

            Assert.Contains(alerts2, a => a.TenantId == tenant2Id && a.Content.Contains(exhausted2Id.ToString(), StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(alerts2, a => a.TenantId == tenant1Id);
        }
    }
}

