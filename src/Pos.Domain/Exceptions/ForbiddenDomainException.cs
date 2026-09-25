namespace Pos.Domain.Exceptions;

public class ForbiddenDomainException : DomainException
{
    public ForbiddenDomainException(string message) : base(message)
    {
    }
}
