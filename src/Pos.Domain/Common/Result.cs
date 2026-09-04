namespace Pos.Domain.Common;

/// <summary>
/// Result Pattern — encapsula el resultado de una operación de negocio que puede fallar
/// de forma esperada, sin lanzar excepciones para flujos de negocio ordinarios.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, DomainError error)
    {
        if (isSuccess && error != DomainError.None)
            throw new InvalidOperationException("Un resultado exitoso no puede contener un error.");

        if (!isSuccess && error == DomainError.None)
            throw new InvalidOperationException("Un resultado fallido debe contener un error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Indica si la operación fue exitosa.</summary>
    public bool IsSuccess { get; }

    /// <summary>Indica si la operación falló.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Error asociado al fallo. Es <see cref="DomainError.None"/> cuando es exitoso.</summary>
    public DomainError Error { get; }

    /// <summary>Crea un resultado exitoso (sin valor).</summary>
    public static Result Success() => new(true, DomainError.None);

    /// <summary>Crea un resultado fallido con el error especificado.</summary>
    public static Result Failure(DomainError error) => new(false, error);

    /// <summary>Crea un Result tipado exitoso con valor.</summary>
    public static Result<TValue> Ok<TValue>(TValue value) => Result<TValue>.Ok(value);

    /// <summary>Crea un Result tipado fallido con error.</summary>
    public static Result<TValue> Fail<TValue>(DomainError error) => Result<TValue>.Fail(error);
}

/// <summary>
/// Result Pattern tipado — encapsula un valor de tipo <typeparamref name="TValue"/>
/// o un error de negocio esperado.
/// </summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    private Result(bool isSuccess, DomainError error, TValue? value) : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// Valor del resultado. Solo accesible si <see cref="Result.IsSuccess"/> es true.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No se puede acceder al valor de un resultado fallido.");

    // Métodos estáticos en la clase concreta (no en el genérico base) — CA1000 fix
    internal static Result<TValue> Ok(TValue value) => new(true, DomainError.None, value);
    internal static Result<TValue> Fail(DomainError error) => new(false, error, default);

    /// <summary>Conversión implícita desde TValue — atajo para el caso exitoso.</summary>
    public static implicit operator Result<TValue>(TValue value) => Ok(value);

    /// <summary>Conversión implícita desde DomainError — atajo para el caso fallido.</summary>
    public static implicit operator Result<TValue>(DomainError error) => Fail(error);
}
