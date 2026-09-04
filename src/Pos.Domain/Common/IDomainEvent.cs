namespace Pos.Domain.Common;

/// <summary>
/// Contrato base para eventos del dominio (Domain Events).
/// Un Domain Event es algo que ocurrió en el pasado dentro del bounded context.
/// </summary>
/// <remarks>
/// <para>Interfaz marcador propia — sin dependencias externas (ADR 0001, ADR 0014).</para>
/// <para>Los Domain Events se resuelven en memoria dentro del mismo proceso
/// y NO deben confundirse con Integration Events, que cruzan límites del sistema
/// vía EventBus (RabbitMQ) — ver Application/IntegrationEvents.</para>
/// </remarks>
public interface IDomainEvent
{
    /// <summary>Fecha y hora UTC en que ocurrió el evento.</summary>
    DateTime OccurredOnUtc { get; }
}
