using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pos.Application.IntegrationEvents.Contracts;
using Pos.Application.IntegrationEvents.Contracts.V1;
using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Infrastructure.Persistence.Outbox;

namespace Pos.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Interceptor de EF Core para el patrón Transactional Outbox (Sección 10).
/// Captura los eventos de dominio producidos por los agregados antes del Commit de la transacción
/// y los transforma a eventos de integración guardados en la tabla OutboxMessages.
/// </summary>
public class InsertOutboxMessagesInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null)
        {
            ConvertDomainEventsToOutboxMessages(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context != null)
        {
            ConvertDomainEventsToOutboxMessages(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private static void ConvertDomainEventsToOutboxMessages(DbContext context)
    {
        var outboxMessages = new List<OutboxMessage>();

        // Obtener todos los agregados que tengan eventos de dominio pendientes
        var entries = context.ChangeTracker
            .Entries()
            .Where(entry => entry.Entity is AggregateRoot<Guid> agg && agg.DomainEvents.Count > 0)
            .ToList();

        foreach (var entry in entries)
        {
            if (entry.Entity is AggregateRoot<Guid> aggregate)
            {
                foreach (var domainEvent in aggregate.DomainEvents)
                {
                    var integrationEvent = MapDomainEventToIntegrationEvent(domainEvent);
                    if (integrationEvent != null)
                    {
                        string jsonContent = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType());
                        var message = OutboxMessage.Create(
                            integrationEvent.Id,
                            integrationEvent.GetType().AssemblyQualifiedName ?? integrationEvent.GetType().Name,
                            jsonContent,
                            integrationEvent.OccurredOnUtc
                        );

                        outboxMessages.Add(message);
                    }
                }
            }
        }

        if (outboxMessages.Count > 0)
        {
            context.Set<OutboxMessage>().AddRange(outboxMessages);
        }
    }

    [SuppressMessage("Quality", "CA1859:Use concrete types when possible for improved performance", Justification = "Se requiere el tipo de interfaz de abstracción IIntegrationEvent para el mapeo polimórfico de eventos de integración.")]
    private static IIntegrationEvent? MapDomainEventToIntegrationEvent(IDomainEvent domainEvent)
    {
        return domainEvent switch
        {
            SaleCompletedDomainEvent saleEvent => new SaleCompletedIntegrationEventV1(
                Guid.NewGuid(),
                saleEvent.SaleId,
                saleEvent.ReceiptNumber,
                saleEvent.TotalAmount,
                saleEvent.Currency,
                saleEvent.OccurredOnUtc
            ),
            _ => null
        };
    }
}
