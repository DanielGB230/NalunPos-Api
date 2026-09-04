namespace Pos.Domain.Common;

/// <summary>
/// Tipo de error de negocio.
/// </summary>
public enum ErrorType
{
    /// <summary>Error de validación de datos de entrada.</summary>
    Validation,

    /// <summary>El recurso solicitado no fue encontrado.</summary>
    NotFound,

    /// <summary>Conflicto de estado (ej: SKU duplicado, stock insuficiente).</summary>
    Conflict,

    /// <summary>El usuario no tiene permisos para la operación.</summary>
    Unauthorized,

    /// <summary>Error de negocio genérico no clasificado en las categorías anteriores.</summary>
    Failure
}

/// <summary>
/// Representa un error de negocio esperado con código, mensaje y tipo semántico.
/// </summary>
/// <remarks>
/// Los errores de negocio esperados usan <see cref="Error"/> + <see cref="Result"/>.
/// Las violaciones de invariantes de dominio que nunca deberían ocurrir
/// usan <see cref="Exceptions.DomainException"/> (lanzada, no retornada).
/// </remarks>
public sealed record DomainError(string Code, string Message, ErrorType Type)
{
    /// <summary>Error vacío — representa ausencia de error (usado en Result.Success).</summary>
    public static readonly DomainError None = new(string.Empty, string.Empty, ErrorType.Failure);

    // --- Factory Methods de conveniencia ---

    public static DomainError Validation(string code, string message)
        => new(code, message, ErrorType.Validation);

    public static DomainError NotFound(string code, string message)
        => new(code, message, ErrorType.NotFound);

    public static DomainError Conflict(string code, string message)
        => new(code, message, ErrorType.Conflict);

    public static DomainError Unauthorized(string code, string message)
        => new(code, message, ErrorType.Unauthorized);

    public static DomainError Failure(string code, string message)
        => new(code, message, ErrorType.Failure);
}
