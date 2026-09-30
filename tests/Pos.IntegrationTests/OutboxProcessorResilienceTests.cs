using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Api.BackgroundServices;
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

    private static OutboxProcessorBackgroundService BuildProcessor(IServiceProvider sp) =>
        new(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OutboxProcessorBackgroundService>>(),
            Microsoft.Extensions.Options.Options.Create(new OutboxSettings { PollingIntervalSeconds = 1 })
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

        var cat = Pos.Domain.Entities.Category.Create($"Cat-Retry-{Guid.NewGuid()}", "Desc");
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
}
