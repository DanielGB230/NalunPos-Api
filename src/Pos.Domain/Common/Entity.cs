namespace Pos.Domain.Common;

/// <summary>
/// Clase base para todas las entidades del dominio.
/// Una entidad posee identidad única que la distingue de otras, independientemente de sus atributos.
/// </summary>
/// <typeparam name="TId">Tipo de identificador único de la entidad.</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    protected Entity(TId id)
    {
        Id = id;
    }

    // Constructor protegido para ORM (Entity Framework Core)
    protected Entity()
    {
    }

    public override bool Equals(object? obj)
    {
        return obj is Entity<TId> entity && Equals(entity);
    }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override int GetHashCode()
    {
        return EqualityComparer<TId>.Default.GetHashCode(Id);
    }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
    {
        return !Equals(left, right);
    }
}

/// <summary>
/// Clase base simplificada para entidades cuya llave primaria es de tipo Guid (UUIDv7).
/// Garantiza la inicialización automática de la identidad utilizando Guid.CreateVersion7() para .NET 10.
/// </summary>
public abstract class Entity : Entity<Guid>
{
    protected Entity(Guid id) : base(id == Guid.Empty ? Guid.CreateVersion7() : id)
    {
    }

    protected Entity() : base(Guid.CreateVersion7())
    {
    }
}

