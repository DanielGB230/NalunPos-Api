namespace Pos.Domain.Enums;

public enum PurchaseStatus
{
    Draft = 1,
    Completed = 2,
    Cancelled = 3
}

public enum PaymentMethod
{
    Cash = 1,
    CreditCard = 2,
    DebitCard = 3,
    Transfer = 4
}

public enum PaymentStatus
{
    Pending = 1,
    Processed = 2,
    Failed = 3
}

public enum InvoiceDocumentType
{
    Invoice = 1, // Factura
    Receipt = 2  // Boleta
}

public enum InvoiceStatus
{
    Pending = 1,
    Sent = 2,
    Accepted = 3,
    Rejected = 4
}
