namespace Pos.Application.Invoicing.Common;

/// <summary>
/// Códigos de error estandarizados para el módulo de facturación (Invoice).
/// </summary>
public static class InvoiceErrors
{
    public const string NotFound = "Invoice.NotFound";
    public const string ConcurrencyConflict = "Invoice.ConcurrencyConflict";
    public const string AlreadyProcessed = "Invoice.AlreadyProcessed";
    public const string ReconciliationExhausted = "Invoice.ReconciliationExhausted";
    public const string AlreadyIssued = "Invoice.AlreadyIssued";
    public const string Invalid = "Invoice.Invalid";
}
