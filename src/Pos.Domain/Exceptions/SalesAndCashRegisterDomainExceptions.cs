namespace Pos.Domain.Exceptions;

public class CustomerNotFoundException : DomainException
{
    public CustomerNotFoundException(Guid customerId)
        : base($"El cliente con ID '{customerId}' no fue encontrado.")
    {
    }
}

public class CashRegisterNotFoundException : DomainException
{
    public CashRegisterNotFoundException(Guid registerId)
        : base($"La caja con ID '{registerId}' no fue encontrada.")
    {
    }
}

public class CashRegisterSessionNotFoundException : DomainException
{
    public CashRegisterSessionNotFoundException(Guid sessionId)
        : base($"La sesión de caja con ID '{sessionId}' no fue encontrada.")
    {
    }
}

public class SaleNotFoundException : DomainException
{
    public SaleNotFoundException(Guid saleId)
        : base($"La venta con ID '{saleId}' no fue encontrada.")
    {
    }
}
