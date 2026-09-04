namespace Pos.Domain.Exceptions;

public class PurchaseNotFoundException : DomainException
{
    public PurchaseNotFoundException(Guid purchaseId)
        : base($"La compra con ID '{purchaseId}' no fue encontrada.")
    {
    }
}

public class PaymentNotFoundException : DomainException
{
    public PaymentNotFoundException(Guid paymentId)
        : base($"El pago con ID '{paymentId}' no fue encontrado.")
    {
    }
}

public class InvoiceNotFoundException : DomainException
{
    public InvoiceNotFoundException(Guid invoiceId)
        : base($"El documento de facturación con ID '{invoiceId}' no fue encontrado.")
    {
    }
}
