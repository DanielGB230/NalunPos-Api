using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pos.Application.IntegrationEvents.Contracts;
using Pos.Application.IntegrationEvents.Contracts.V1;
using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Entities;
using Pos.Infrastructure.Persistence.Outbox;

namespace Pos.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Interceptor de EF Core para el patrón Transactional Outbox (Sección 10).
/// Captura los eventos de dominio producidos por los agregados antes del Commit de la transacción
/// y los transforma a eventos de integración guardados en la tabla OutboxMessages.
/// </summary>
public class InsertOutboxMessagesInterceptor : SaveChangesInterceptor
{
    private readonly Pos.Application.Common.Interfaces.ICurrentTenantContext _tenantContext;
    private readonly HashSet<IDomainEvent> _processedEvents = new(ReferenceEqualityComparer.Instance);
    private readonly List<IDomainEvent> _currentAttemptEvents = new();

    public InsertOutboxMessagesInterceptor(Pos.Application.Common.Interfaces.ICurrentTenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null)
        {
            ConvertDomainEventsToOutboxMessages(eventData.Context, _tenantContext.TenantId);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context != null)
        {
            ConvertDomainEventsToOutboxMessages(eventData.Context, _tenantContext.TenantId);
        }

        return base.SavingChanges(eventData, result);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _processedEvents.ExceptWith(_currentAttemptEvents);
        _currentAttemptEvents.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _processedEvents.ExceptWith(_currentAttemptEvents);
        _currentAttemptEvents.Clear();
        base.SaveChangesFailed(eventData);
    }

    private void ConvertDomainEventsToOutboxMessages(DbContext context, Guid? currentTenantId)
    {
        _currentAttemptEvents.Clear();
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
                    if (_processedEvents.Contains(domainEvent))
                    {
                        continue;
                    }

                    var integrationEvent = MapDomainEventToIntegrationEvent(domainEvent);
                    if (integrationEvent != null)
                    {
                        var effectiveTenantId = (domainEvent as ITenantTargetedDomainEvent)?.TenantId
                            ?? currentTenantId
                            ?? (aggregate as ITenantOwnedEntity)?.TenantId
                            ?? (aggregate as IOptionalTenantOwnedEntity)?.TenantId;

                        string jsonContent = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType());
                        var message = OutboxMessage.Create(
                            integrationEvent.Id,
                            effectiveTenantId,
                            integrationEvent.GetType().AssemblyQualifiedName ?? integrationEvent.GetType().Name,
                            jsonContent,
                            integrationEvent.OccurredOnUtc
                        );

                        outboxMessages.Add(message);
                        _processedEvents.Add(domainEvent);
                        _currentAttemptEvents.Add(domainEvent);
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
            UserRoleChangedDomainEvent userRoleEvent => new UserRoleChangedIntegrationEventV1(
                Guid.NewGuid(),
                userRoleEvent.UserId,
                userRoleEvent.OldRoleId,
                userRoleEvent.NewRoleId,
                userRoleEvent.OldTenantId,
                userRoleEvent.NewTenantId,
                userRoleEvent.OccurredOnUtc
            ),
            UserStatusChangedDomainEvent userStatusEvent => new UserStatusChangedIntegrationEventV1(
                Guid.NewGuid(),
                userStatusEvent.UserId,
                userStatusEvent.TenantId,
                userStatusEvent.IsActive,
                userStatusEvent.OccurredOnUtc
            ),
            TenantStatusChangedDomainEvent tenantStatusEvent => new TenantStatusChangedIntegrationEventV1(
                Guid.NewGuid(),
                tenantStatusEvent.TenantId,
                tenantStatusEvent.OldStatus.ToString(),
                tenantStatusEvent.NewStatus.ToString(),
                tenantStatusEvent.OccurredOnUtc
            ),
            ProductStatusChangedDomainEvent productStatusEvent => new ProductStatusChangedIntegrationEventV1(
                Guid.NewGuid(),
                productStatusEvent.ProductId,
                productStatusEvent.TenantId,
                productStatusEvent.IsActive,
                productStatusEvent.OccurredOnUtc
            ),
            PurchaseOrderSentDomainEvent poSentEvent => new PurchaseOrderSentIntegrationEventV1(
                Guid.NewGuid(),
                poSentEvent.PurchaseOrderId,
                poSentEvent.TenantId,
                poSentEvent.SupplierId,
                poSentEvent.WarehouseId,
                poSentEvent.OrderNumber,
                poSentEvent.OccurredOnUtc
            ),
            PurchaseOrderCancelledDomainEvent poCancelledEvent => new PurchaseOrderCancelledIntegrationEventV1(
                Guid.NewGuid(),
                poCancelledEvent.PurchaseOrderId,
                poCancelledEvent.TenantId,
                poCancelledEvent.SupplierId,
                poCancelledEvent.WarehouseId,
                poCancelledEvent.OrderNumber,
                poCancelledEvent.PreviousStatus.ToString(),
                poCancelledEvent.OccurredOnUtc
            ),
            _ => null
        };
    }
}
