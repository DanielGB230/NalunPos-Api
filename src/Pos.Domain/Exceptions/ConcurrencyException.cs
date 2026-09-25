namespace Pos.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando ocurre un conflicto de concurrencia optimista al guardar cambios.
/// </summary>
public class ConcurrencyException : DomainException
{
    public ConcurrencyException(string message) : base(message)
    {
    }

    public ConcurrencyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
