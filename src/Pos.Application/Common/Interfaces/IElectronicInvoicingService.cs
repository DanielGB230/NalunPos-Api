using Pos.Domain.Entities;

namespace Pos.Application.Common.Interfaces;

public record InvoicingServiceResult(
    bool IsSuccess,
    string DocumentNumber,
    string? ResponseHash,
    string? ErrorMessage
);

/// <summary>
/// Contrato agnóstico de la Capa Anti-Corrupción (ACL) para servicios de facturación electrónica externa (SUNAT, SAT, SII, etc).
/// </summary>
public interface IElectronicInvoicingService
{
    Task<InvoicingServiceResult> SendInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default);
}
