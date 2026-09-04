namespace Pos.Domain.Exceptions;

public class SupplierNotFoundException : DomainException
{
    public SupplierNotFoundException(Guid supplierId)
        : base($"El proveedor con ID '{supplierId}' no fue encontrado.")
    {
    }
}
