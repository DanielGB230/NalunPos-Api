namespace Pos.Api.Contracts.Requests;

public record IssueInvoiceRequest(
    Guid SaleId,
    Pos.Domain.Enums.InvoiceDocumentType DocumentType,
    string CustomerTaxIdValue,
    string CustomerTaxCountryCode
);
