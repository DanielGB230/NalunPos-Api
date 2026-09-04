using Pos.Domain.Entities;

namespace Pos.Application.Invoicing.DTOs;

public record InvoiceDto(
    Guid Id,
    Guid SaleId,
    string DocumentTypeName,
    string DocumentNumber,
    string CustomerTaxId,
    string CustomerTaxCountryCode,
    decimal TotalAmount,
    string Currency,
    string StatusName,
    DateTime IssueDateUtc
)
{
    public static InvoiceDto FromEntity(Invoice invoice)
    {
        return new InvoiceDto(
            invoice.Id,
            invoice.SaleId,
            invoice.DocumentType.ToString(),
            invoice.DocumentNumber,
            invoice.CustomerTaxId.Value,
            invoice.CustomerTaxId.CountryCode,
            invoice.TotalAmount.Amount,
            invoice.TotalAmount.Currency,
            invoice.Status.ToString(),
            invoice.IssueDateUtc
        );
    }
}
