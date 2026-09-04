namespace Pos.Domain.Common;

/// <summary>
/// Clase base para agregados (Aggregate Roots) en DDD.
/// Representa la raíz de consistencia transaccional y es responsable de emitir eventos de dominio.
/// </summary>
/// <typeparam name="TId">Tipo de identificador único del agregado.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id) : base(id)
    {
    }

    protected AggregateRoot()
    {
    }

    /// <summary>
    /// Lista de eventos de dominio acumulados que no han sido publicados aún.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Registra un evento de dominio para ser publicado cuando la transacción se guarde.
    /// </summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Limpia los eventos de dominio una vez que han sido despachados.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

/// <summary>
/// Clase base simplificada para agregados cuya llave primaria es de tipo Guid (UUIDv7).
/// </summary>
public abstract class AggregateRoot : AggregateRoot<Guid>
{
    protected AggregateRoot(Guid id) : base(id == Guid.Empty ? Guid.CreateVersion7() : id)
    {
    }

    protected AggregateRoot() : base(Guid.CreateVersion7())
    {
    }
}

