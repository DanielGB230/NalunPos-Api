namespace Pos.IntegrationTests;

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pos.Api.BackgroundServices;
using Pos.Api.Options;
using Pos.Application.Common.Interfaces;
using Pos.Application.IntegrationEvents.Contracts;
using Pos.Application.Platform.Outbox.Commands;
using Pos.Application.Platform.Outbox.Queries;
using Pos.Infrastructure.Persistence.Outbox;
using Pos.IntegrationTests.Fixtures;
using Xunit;

[Collection("IntegrationTests")]
public class OutboxProcessorConcurrencyIntegrationTests
{
    private readonly MsSqlTestFixture _fixture;

    public OutboxProcessorConcurrencyIntegrationTests(MsSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task OutboxProcessor_WhenTwoReplicasRunConcurrently_NeverDeliversSameMessageTwice()
    {
        // 1. Arrange: Sembrar 20 mensajes pendientes de outbox en SQL Server
        var tenantId = Guid.NewGuid();
        var payloads = Enumerable.Range(0, 20).Select(i => $"ConcurrentPayload-{Guid.NewGuid():N}-{i}").ToList();

        using (var db = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            foreach (var payload in payloads)
            {
                var evt = new ConcurrentOutboxIntegrationEvent(Guid.NewGuid(), payload);
                var content = JsonSerializer.Serialize(evt);
                var message = OutboxMessage.Create(
                    Guid.NewGuid(),
                    tenantId,
                    typeof(ConcurrentOutboxIntegrationEvent).AssemblyQualifiedName!,
                    content,
                    DateTimeOffset.UtcNow);

                db.OutboxMessages.Add(message);
            }
            await db.SaveChangesAsync();
        }

        // Bus espía concurrente compartido entre ambas réplicas
        var spyBus = new ConcurrentSpyEventBus();

        var sp1 = _fixture.CreateServiceProvider(configureServices: services =>
        {
            services.AddScoped<IEventBus>(_ => spyBus);
        });

        var sp2 = _fixture.CreateServiceProvider(configureServices: services =>
        {
            services.AddScoped<IEventBus>(_ => spyBus);
        });

        var settings = Options.Create(new OutboxSettings
        {
            BatchSize = 10,
            PollingIntervalSeconds = 5,
            LeaseDurationSeconds = 30,
            MaxRetries = 3
        });

        var worker1 = new OutboxProcessorBackgroundService(
            sp1.GetRequiredService<IServiceScopeFactory>(),
            sp1.GetRequiredService<ILogger<OutboxProcessorBackgroundService>>(),
            settings,
            TimeProvider.System);

        var worker2 = new OutboxProcessorBackgroundService(
            sp2.GetRequiredService<IServiceScopeFactory>(),
            sp2.GetRequiredService<ILogger<OutboxProcessorBackgroundService>>(),
            settings,
            TimeProvider.System);

        // 2. Act: Ejecutar ambos workers simultáneamente en paralelo hasta vaciar los mensajes sembrados
        for (int attempt = 0; attempt < 10; attempt++)
        {
            await Task.WhenAll(
                worker1.ProcessOutboxMessagesAsync(CancellationToken.None),
                worker2.ProcessOutboxMessagesAsync(CancellationToken.None));

            using var checkDb = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true);
            var pending = await checkDb.OutboxMessages
                .CountAsync(m => m.ProcessedOnUtc == null && payloads.Any(p => m.Content.Contains(p)));
            if (pending == 0)
            {
                break;
            }
        }

        // 3. Assert: Ningún mensaje debe haber sido entregado más de una vez
        foreach (var payload in payloads)
        {
            Assert.True(spyBus.PublishCounts.ContainsKey(payload), $"El payload {payload} no fue entregado.");
            Assert.Equal(1, spyBus.PublishCounts[payload]); // Exactamente 1 entrega
        }

        using (var verifyDb = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            var processedCount = await verifyDb.OutboxMessages
                .Where(m => m.ProcessedOnUtc != null && payloads.Any(p => m.Content.Contains(p)))
                .CountAsync();

            Assert.Equal(20, processedCount);

            var lockedCount = await verifyDb.OutboxMessages
                .Where(m => m.LockedUntilUtc != null && payloads.Any(p => m.Content.Contains(p)))
                .CountAsync();

            Assert.Equal(0, lockedCount); // Todos los leases fueron liberados tras el éxito
        }
    }

    [Fact]
    public async Task DeadLetter_QueryAndReprocess_AllowsPlatformInspectionAndRecovery()
    {
        // 1. Arrange: Sembrar 2 mensajes de Outbox en estado Dead-Letter
        var tenantId = Guid.NewGuid();
        var messageId1 = Guid.NewGuid();
        var messageId2 = Guid.NewGuid();

        using (var db = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            var msg1 = OutboxMessage.Create(messageId1, tenantId, "TestTypeA", "{\"test\":1}", DateTimeOffset.UtcNow);
            msg1.MarkAsDeadLetter("Error simulado permanente 1");

            var msg2 = OutboxMessage.Create(messageId2, tenantId, "TestTypeB", "{\"test\":2}", DateTimeOffset.UtcNow);
            msg2.MarkAsDeadLetter("Error simulado permanente 2");

            db.OutboxMessages.AddRange(msg1, msg2);
            await db.SaveChangesAsync();
        }

        // Crear contexto con permisos de plataforma (SuperAdmin)
        var sp = _fixture.CreateServiceProvider(tenantId: null, isSuperAdmin: true);
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        // 2. Act & Assert: Consultar vía GetDeadLetterMessagesQuery
        var queryResult = await dispatcher.SendAsync(new GetDeadLetterMessagesQuery(Take: 100));
        Assert.True(queryResult.IsSuccess);
        Assert.Contains(queryResult.Value, m => m.Id == messageId1);
        Assert.Contains(queryResult.Value, m => m.Id == messageId2);

        // 3. Act: Reprocesar vía ReprocessDeadLetterMessagesCommand
        var reprocessResult = await dispatcher.SendAsync(new ReprocessDeadLetterMessagesCommand([messageId1, messageId2]));
        Assert.True(reprocessResult.IsSuccess);
        Assert.Equal(2, reprocessResult.Value);

        // 4. Assert: Verificar en la base de datos que ya no están en Dead-Letter y pueden reintentarse
        using (var verifyDb = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            var updated1 = await verifyDb.OutboxMessages.FindAsync(messageId1);
            var updated2 = await verifyDb.OutboxMessages.FindAsync(messageId2);

            Assert.NotNull(updated1);
            Assert.False(updated1.IsDeadLetter);
            Assert.Equal(0, updated1.RetryCount);
            Assert.Null(updated1.Error);
            Assert.Null(updated1.LockedUntilUtc);

            Assert.NotNull(updated2);
            Assert.False(updated2.IsDeadLetter);
            Assert.Equal(0, updated2.RetryCount);
            Assert.Null(updated2.Error);
            Assert.Null(updated2.LockedUntilUtc);
        }
    }

    private sealed class ConcurrentSpyEventBus : IEventBus
    {
        public ConcurrentDictionary<string, int> PublishCounts { get; } = new();

        public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        {
            if (integrationEvent is ConcurrentOutboxIntegrationEvent concurrent)
            {
                PublishCounts.AddOrUpdate(concurrent.Payload, 1, (_, current) => current + 1);
            }
            return Task.CompletedTask;
        }
    }
}

public sealed record ConcurrentOutboxIntegrationEvent(Guid EventId, string Payload) : IIntegrationEvent
{
    public Guid Id => EventId;
    public DateTimeOffset OccurredOnUtc => DateTimeOffset.UtcNow;
}
