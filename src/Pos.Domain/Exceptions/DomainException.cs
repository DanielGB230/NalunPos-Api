namespace Pos.Domain.Exceptions;

/// <summary>
/// Excepción base e instanciable para violaciones de reglas de negocio del dominio.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
