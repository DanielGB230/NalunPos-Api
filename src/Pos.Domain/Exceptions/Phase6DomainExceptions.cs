namespace Pos.Domain.Exceptions;

public class UserNotFoundException : DomainException
{
    public UserNotFoundException(Guid userId)
        : base($"El usuario con ID '{userId}' no fue encontrado.")
    {
    }

    public UserNotFoundException(string email)
        : base($"No se encontró ningún usuario con el correo '{email}'.")
    {
    }
}

public class RoleNotFoundException : DomainException
{
    public RoleNotFoundException(Guid roleId)
        : base($"El rol con ID '{roleId}' no fue encontrado.")
    {
    }
}

public class UnauthorizedDomainException : DomainException
{
    public UnauthorizedDomainException(string message = "No tiene permisos suficientes para realizar esta acción.")
        : base(message)
    {
    }
}
