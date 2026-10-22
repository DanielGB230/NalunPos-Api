using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Api.BackgroundServices;
using Pos.Api.Options;
using Pos.Application.IntegrationEvents.Contracts;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Outbox;
using Pos.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Pos.IntegrationTests;

/// <summary>
/// Tests de resiliencia y comportamiento del OutboxProcessorBackgroundService.
/// Cubre: tipo no resoluble, deserialización fallida, tipo no-IIntegrationEvent,
/// y garantía de no-duplicación de OutboxMessages ante SaveChanges fallido/reintento.
/// </summary>
[Collection("IntegrationTests")]
public class OutboxProcessorResilienceTests
{
    private readonly MsSqlTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public OutboxProcessorResilienceTests(MsSqlTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private static OutboxProcessorBackgroundService BuildProcessor(IServiceProvider sp, OutboxSettings? settings = null, TimeProvider? timeProvider = null) =>
        new(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OutboxProcessorBackgroundService>>(),
            Microsoft.Extensions.Options.Options.Create(settings ?? new OutboxSettings { PollingIntervalSeconds = 1, BaseRetryIntervalSeconds = 2, MaxRetries = 3 }),
            timeProvider ?? TimeProvider.System
        );

    // ────────────────────────────────────────────────────────────────────────────
    // Punto 4: Tipos inválidos/no resolubles
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OutboxProcessor_UnresolvableType_MarksMessageAsFailed()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Guid messageId = Guid.NewGuid();

        using (var db = _fixture.CreateDbContext(tenantId))
        {
            var message = OutboxMessage.Create(
                messageId,
                tenantId,
                "Pos.Application.DoesNotExist.SomeEvent, Pos.Application",  // tipo que no existe
                "{\"Id\":\"" + Guid.NewGuid() + "\"}",
                DateTimeOffset.UtcNow.AddYears(-5)
            );
            db.OutboxMessages.Add(message);
            await db.SaveChangesAsync();
        }

        var sp = _fixture.CreateServiceProvider(null);
        var processor = BuildProcessor(sp);

        // Act
        await processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        // Assert: el mensaje debe estar marcado como fallido, NO como procesado
        using var dbVerify = _fixture.CreateDbContext(null, isSuperAdmin: true);
        var msg = await dbVerify.OutboxMessages.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == messageId);

        Assert.NotNull(msg);
        Assert.Null(msg.ProcessedOnUtc);
        Assert.NotNull(msg.Error);
        Assert.Contains("No se pudo resolver el tipo", msg.Error);

        _output.WriteLine($"[OK] Error registrado: {msg.Error}");
    }

    [Fact]
    public async Task OutboxProcessor_MalformedJson_MarksMessageAsFailed()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Guid messageId = Guid.NewGuid();

        using (var db = _fixture.CreateDbContext(tenantId))
        {
            var message = OutboxMessage.Create(
                messageId,
                tenantId,
                typeof(DummyTenantIntegrationEvent).AssemblyQualifiedName!,
                "ESTE_NO_ES_JSON_VALIDO_{{{",  // JSON malformado
                DateTimeOffset.UtcNow.AddYears(-5)
            );
            db.OutboxMessages.Add(message);
            await db.SaveChangesAsync();
        }

        var sp = _fixture.CreateServiceProvider(null);
        var processor = BuildProcessor(sp);

        // Act
        await processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        // Assert: marcado como fallido, no procesado
        using var dbVerify = _fixture.CreateDbContext(null, isSuperAdmin: true);
        var msg = await dbVerify.OutboxMessages.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == messageId);

        Assert.NotNull(msg);
        Assert.Null(msg.ProcessedOnUtc);
        Assert.NotNull(msg.Error);
        Assert.Contains("deserialización", msg.Error, StringComparison.OrdinalIgnoreCase);

        _output.WriteLine($"[OK] Error de deserialización registrado: {msg.Error}");
    }

    [Fact]
    public async Task OutboxProcessor_TypeNotImplementingIIntegrationEvent_MarksMessageAsFailed()
    {
        // Arrange: tipo que existe pero NO implementa IIntegrationEvent
        Guid tenantId = Guid.NewGuid();
        Guid messageId = Guid.NewGuid();

        using (var db = _fixture.CreateDbContext(tenantId))
        {
            var message = OutboxMessage.Create(
                messageId,
                tenantId,
                typeof(string).AssemblyQualifiedName!,  // System.String no implementa IIntegrationEvent
                "\"hello\"",
                DateTimeOffset.UtcNow.AddYears(-5)
            );
            db.OutboxMessages.Add(message);
            await db.SaveChangesAsync();
        }

        var sp = _fixture.CreateServiceProvider(null);
        var processor = BuildProcessor(sp);

        // Act
        await processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        // Assert: marcado como fallido
        using var dbVerify = _fixture.CreateDbContext(null, isSuperAdmin: true);
        var msg = await dbVerify.OutboxMessages.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == messageId);

        Assert.NotNull(msg);
        Assert.Null(msg.ProcessedOnUtc);
        Assert.NotNull(msg.Error);
        Assert.Contains("IIntegrationEvent", msg.Error);

        _output.WriteLine($"[OK] Error de tipo incorrecto registrado: {msg.Error}");
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Punto 5: SaveChanges fallido → NO duplica OutboxMessages
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OutboxMessage_WhenSaveChangesCalledTwiceOnSameContext_DoesNotDuplicateOutboxMessages()
    {
        Guid tenantId = Guid.NewGuid();

        using var db = _fixture.CreateDbContext(tenantId);

        var cat = Pos.Domain.Entities.Category.Create(tenantId, $"Cat-Retry-{Guid.NewGuid()}", "Desc");
        db.Categories.Add(cat);

        // Primera llamada: guardado exitoso (el interceptor convierte DomainEvents → OutboxMessages)
        await db.SaveChangesAsync();

        // Obtener cuántos OutboxMessages se crearon tras el primer guardado
        int countAfterFirst;
        using (var dbCheck = _fixture.CreateDbContext(null, isSuperAdmin: true))
        {
            countAfterFirst = await dbCheck.OutboxMessages.IgnoreQueryFilters()
                .Where(m => m.TenantId == tenantId)
                .CountAsync();
        }
        _output.WriteLine($"[OK] OutboxMessages tras primer SaveChanges: {countAfterFirst}");

        // Segunda llamada al mismo DbContext (simula reintento o doble llamada):
        // los DomainEvents ya fueron limpiados en SavedChanges, así que no se debe añadir nada nuevo
        await db.SaveChangesAsync();

        int countAfterSecond;
        using (var dbCheck = _fixture.CreateDbContext(null, isSuperAdmin: true))
        {
            countAfterSecond = await dbCheck.OutboxMessages.IgnoreQueryFilters()
                .Where(m => m.TenantId == tenantId)
                .CountAsync();
        }
        _output.WriteLine($"[OK] OutboxMessages tras segundo SaveChanges: {countAfterSecond}");

        // Assert: el segundo SaveChanges NO duplicó los OutboxMessages
        Assert.Equal(countAfterFirst, countAfterSecond);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Punto 6: Reintentos con Backoff Exponencial y Dead-Letter
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OutboxProcessor_WhenMessageFails_IncrementsRetryCountAndCalculatesBackoff()
    {
        Guid tenantId = Guid.NewGuid();
        Guid messageId = Guid.NewGuid();

        using (var db = _fixture.CreateDbContext(tenantId))
        {
            var message = OutboxMessage.Create(
                messageId,
                tenantId,
                "Pos.Application.DoesNotExist.FailingEvent, Pos.Application",
                "{\"Id\":\"" + Guid.NewGuid() + "\"}",
                DateTimeOffset.UtcNow.AddYears(-5)
            );
            db.OutboxMessages.Add(message);
            await db.SaveChangesAsync();
        }

        var sp = _fixture.CreateServiceProvider(null);
        var settings = new OutboxSettings
        {
            PollingIntervalSeconds = 1,
            MaxRetries = 3,
            BaseRetryIntervalSeconds = 2
        };
        var processor = BuildProcessor(sp, settings);

        // Act
        var beforeProcess = DateTimeOffset.UtcNow;
        await processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        // Assert: RetryCount incrementado a 1, no es dead-letter y NextRetryAtUtc programado con backoff
        using var dbVerify = _fixture.CreateDbContext(null, isSuperAdmin: true);
        var msg = await dbVerify.OutboxMessages.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == messageId);

        Assert.NotNull(msg);
        Assert.Null(msg.ProcessedOnUtc);
        Assert.NotNull(msg.Error);
        Assert.Equal(1, msg.RetryCount);
        Assert.False(msg.IsDeadLetter);
        Assert.NotNull(msg.NextRetryAtUtc);
        // Delay esperado: 2^0 * 2s = 2 segundos
        Assert.True(msg.NextRetryAtUtc >= beforeProcess.AddSeconds(1));
    }

    [Fact]
    public async Task OutboxProcessor_WhenNextRetryInFuture_DoesNotReprocessUntilDue()
    {
        Guid tenantId = Guid.NewGuid();
        Guid messageId = Guid.NewGuid();

        using (var db = _fixture.CreateDbContext(tenantId))
        {
            var message = OutboxMessage.Create(
                messageId,
                tenantId,
                "Pos.Application.DoesNotExist.FailingEvent, Pos.Application",
                "{\"Id\":\"" + Guid.NewGuid() + "\"}",
                DateTimeOffset.UtcNow.AddYears(-5)
            );
            // Simular primer fallo previo con próximo reintento en el futuro (+10 minutos)
            message.RecordFailure("Error temporal", maxRetries: 3, retryDelay: TimeSpan.FromMinutes(10), TimeProvider.System);
            db.OutboxMessages.Add(message);
            await db.SaveChangesAsync();
        }

        var sp = _fixture.CreateServiceProvider(null);
        var processor = BuildProcessor(sp);

        // Act 1: Como NextRetryAtUtc está en el futuro, no debe procesarlo
        await processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        using (var dbVerify1 = _fixture.CreateDbContext(null, isSuperAdmin: true))
        {
            var msg1 = await dbVerify1.OutboxMessages.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == messageId);
            Assert.NotNull(msg1);
            Assert.Equal(1, msg1.RetryCount); // No se incrementó
        }

        // Act 2: Actualizar NextRetryAtUtc al pasado (simulando que el tiempo transcurrió)
        using (var dbUpdate = _fixture.CreateDbContext(null, isSuperAdmin: true))
        {
            await dbUpdate.Database.ExecuteSqlAsync($"UPDATE OutboxMessages SET NextRetryAtUtc = {DateTimeOffset.UtcNow.AddSeconds(-5)} WHERE Id = {messageId}");
        }

        // Act 3: Ahora sí debe ser seleccionado para reintento
        await processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        using (var dbVerify2 = _fixture.CreateDbContext(null, isSuperAdmin: true))
        {
            var msg2 = await dbVerify2.OutboxMessages.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == messageId);
            Assert.NotNull(msg2);
            Assert.Equal(2, msg2.RetryCount); // Se incrementó al segundo reintento
        }
    }

    [Fact]
    public async Task OutboxProcessor_WhenMaxRetriesReached_TransitionsToDeadLetter()
    {
        Guid tenantId = Guid.NewGuid();
        Guid messageId = Guid.NewGuid();

        using (var db = _fixture.CreateDbContext(tenantId))
        {
            var message = OutboxMessage.Create(
                messageId,
                tenantId,
                "Pos.Application.DoesNotExist.FailingEvent, Pos.Application",
                "{\"Id\":\"" + Guid.NewGuid() + "\"}",
                DateTimeOffset.UtcNow.AddYears(-5)
            );
            db.OutboxMessages.Add(message);
            await db.SaveChangesAsync();
        }

        var sp = _fixture.CreateServiceProvider(null);
        var settings = new OutboxSettings
        {
            PollingIntervalSeconds = 1,
            MaxRetries = 2, // Límite de 2 reintentos
            BaseRetryIntervalSeconds = 1
        };
        var processor = BuildProcessor(sp, settings);

        // Intento 1: falla y se programa reintento
        await processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        using (var dbCheck1 = _fixture.CreateDbContext(null, isSuperAdmin: true))
        {
            var msg1 = await dbCheck1.OutboxMessages.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == messageId);
            Assert.NotNull(msg1);
            Assert.Equal(1, msg1.RetryCount);
            Assert.False(msg1.IsDeadLetter);
            Assert.NotNull(msg1.NextRetryAtUtc);
        }

        // Simular que el intervalo expiró
        using (var dbUpdate = _fixture.CreateDbContext(null, isSuperAdmin: true))
        {
            await dbUpdate.Database.ExecuteSqlAsync($"UPDATE OutboxMessages SET NextRetryAtUtc = {DateTimeOffset.UtcNow.AddSeconds(-1)} WHERE Id = {messageId}");
        }

        // Intento 2: alcanza MaxRetries (2) -> pasa a Dead-Letter
        await processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        using (var dbCheck2 = _fixture.CreateDbContext(null, isSuperAdmin: true))
        {
            var msg2 = await dbCheck2.OutboxMessages.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == messageId);
            Assert.NotNull(msg2);
            Assert.Equal(2, msg2.RetryCount);
            Assert.True(msg2.IsDeadLetter);
            Assert.Null(msg2.NextRetryAtUtc);
            Assert.NotNull(msg2.Error);
        }

        // Ejecución posterior: el mensaje dead-letter ya no es tomado por el worker
        await processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        using (var dbCheck3 = _fixture.CreateDbContext(null, isSuperAdmin: true))
        {
            var msg3 = await dbCheck3.OutboxMessages.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == messageId);
            Assert.NotNull(msg3);
            Assert.Equal(2, msg3.RetryCount); // Se mantiene en 2, ignorado
            Assert.True(msg3.IsDeadLetter);
        }
    }
}
