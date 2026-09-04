using System.Diagnostics.CodeAnalysis;

namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Handler de Domain Events — ejecuta side effects en respuesta a un evento
/// que ocurrió en el dominio (ej: descontar stock cuando se completa una venta).
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Convención de nombre estándar para manejadores de eventos del dominio en CQRS.")]
public interface IDomainEventHandler<in TDomainEvent>
    where TDomainEvent : notnull
{
    Task HandleAsync(TDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
