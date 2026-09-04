using Pos.Domain.Common;
using Xunit;

namespace Pos.Domain.Tests;

// ─── Tipos de prueba (concretos mínimos para los tests) ─────────────────────

/// <summary>Entidad concreta para tests — usa Entity<Guid> (genérico).</summary>
file sealed class TestEntityGeneric : Entity<Guid>
{
    public TestEntityGeneric(Guid id) : base(id) { }
}

/// <summary>Entidad concreta para tests — usa Entity (alias con UUIDv7).</summary>
file sealed class TestEntity : Entity
{
    public TestEntity(Guid id) : base(id) { }
    public TestEntity() : base() { }
}

/// <summary>Aggregate Root concreto para tests.</summary>
file sealed class TestAggregate : AggregateRoot
{
    public TestAggregate() : base() { }
    public void RaiseTestEvent() => RaiseDomainEvent(new TestDomainEvent());
}

/// <summary>Domain Event mínimo para tests.</summary>
file sealed record TestDomainEvent : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

/// <summary>Value Object concreto para tests.</summary>
file sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency) { Amount = amount; Currency = currency; }
    public static Money Of(decimal amount, string currency) => new(amount, currency);

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Amount;
        yield return Currency;
    }
}

/// <summary>Resultado de negocio concreto para tests del Result Pattern.</summary>
file sealed record OrderId(Guid Value);

// ─── Tests ──────────────────────────────────────────────────────────────────

public class DomainCommonBuildingBlockTests
{
    // --- Entity<TId> genérico ---

    [Fact]
    public void Entity_WithSameId_ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var a = new TestEntityGeneric(id);
        var b = new TestEntityGeneric(id);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Entity_WithDifferentId_ShouldNotBeEqual()
    {
        var a = new TestEntityGeneric(Guid.NewGuid());
        var b = new TestEntityGeneric(Guid.NewGuid());

        Assert.NotEqual(a, b);
        Assert.False(a == b);
    }

    // --- Entity (alias Guid + UUIDv7) ---

    [Fact]
    public void Entity_DefaultConstructor_ShouldAssignNonEmptyUuidV7()
    {
        var entity = new TestEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    [Fact]
    public void Entity_TwoDefaultConstructors_ShouldHaveDifferentIds()
    {
        var a = new TestEntity();
        var b = new TestEntity();

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void Entity_WithSameGuidId_ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var a = new TestEntity(id);
        var b = new TestEntity(id);

        Assert.Equal(a, b);
    }

    // --- AggregateRoot: Domain Events ---

    [Fact]
    public void AggregateRoot_RaiseDomainEvent_ShouldAccumulateEvent()
    {
        var aggregate = new TestAggregate();

        aggregate.RaiseTestEvent();

        Assert.Single(aggregate.DomainEvents);
        Assert.IsType<TestDomainEvent>(aggregate.DomainEvents.First());
    }

    [Fact]
    public void AggregateRoot_RaiseMultipleEvents_ShouldAccumulateAll()
    {
        var aggregate = new TestAggregate();

        aggregate.RaiseTestEvent();
        aggregate.RaiseTestEvent();
        aggregate.RaiseTestEvent();

        Assert.Equal(3, aggregate.DomainEvents.Count);
    }

    [Fact]
    public void AggregateRoot_ClearDomainEvents_ShouldEmptyCollection()
    {
        var aggregate = new TestAggregate();
        aggregate.RaiseTestEvent();
        aggregate.RaiseTestEvent();

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }

    // --- ValueObject: igualdad estructural ---

    [Fact]
    public void ValueObject_WithSameComponents_ShouldBeEqual()
    {
        var a = Money.Of(100m, "USD");
        var b = Money.Of(100m, "USD");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueObject_WithDifferentComponents_ShouldNotBeEqual()
    {
        var a = Money.Of(100m, "USD");
        var b = Money.Of(100m, "EUR");

        Assert.NotEqual(a, b);
        Assert.False(a == b);
    }

    // --- Result Pattern ---

    [Fact]
    public void Result_Success_IsSuccessTrue_ErrorIsNone()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(DomainError.None, result.Error);
    }

    [Fact]
    public void Result_Failure_IsSuccessFalse_ContainsError()
    {
        var error = DomainError.NotFound("Product.NotFound", "Producto no encontrado.");
        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void ResultT_Success_ValueIsAccessible()
    {
        var orderId = new OrderId(Guid.NewGuid());
        var result = Result.Ok(orderId);

        Assert.True(result.IsSuccess);
        Assert.Equal(orderId, result.Value);
    }

    [Fact]
    public void ResultT_Failure_AccessingValueThrowsInvalidOperation()
    {
        var result = Result.Fail<OrderId>(DomainError.Failure("Order.Error", "Fallo de prueba."));

        Assert.False(result.IsSuccess);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void ResultT_ImplicitConversionFromValue_IsSuccess()
    {
        Result<OrderId> result = new OrderId(Guid.NewGuid());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ResultT_ImplicitConversionFromError_IsFailure()
    {
        Result<OrderId> result = DomainError.Conflict("Order.Conflict", "Conflicto de prueba.");

        Assert.True(result.IsFailure);
    }

    // --- IDomainEvent: sin dependencias de MediatR ---

    [Fact]
    public void IDomainEvent_IsNotDependentOnMediatR()
    {
        var domainEventType = typeof(IDomainEvent);
        var interfaces = domainEventType.GetInterfaces();

        Assert.DoesNotContain(interfaces, i => i.FullName?.Contains("MediatR") == true);
        Assert.Empty(interfaces); // IDomainEvent es interfaz pura, sin herencia
    }
}
