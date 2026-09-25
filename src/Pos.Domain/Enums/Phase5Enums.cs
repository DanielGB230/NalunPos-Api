namespace Pos.Domain.Enums;

// NOTA: PurchaseStatus fue eliminado junto con la entidad Purchase (legacy).
// El ciclo de vida de órdenes de compra está ahora en PurchaseOrderStatus (InventoryEnums.cs).
// Ver ADR-Inventory-001.

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

public enum ElectronicInvoiceProviderStatus
{
    Accepted = 1,
    Rejected = 2,
    NotFound = 3
}
