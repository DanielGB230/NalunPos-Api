using Pos.Domain.Entities;
using Pos.Domain.Enums;

namespace Pos.Application.Common.Interfaces;

public record InvoicingServiceResult(
    ElectronicInvoiceProviderStatus Status,
    string DocumentNumber,
    string? ResponseHash,
    string? ErrorMessage
)
{
    public bool IsSuccess => Status == ElectronicInvoiceProviderStatus.Accepted;
}

/// <summary>
/// Contrato agnóstico de la Capa Anti-Corrupción (ACL) para servicios de facturación electrónica externa (SUNAT, SAT, SII, etc).
/// </summary>
public interface IElectronicInvoicingService
{
    Task<InvoicingServiceResult> SendInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task<InvoicingServiceResult> GetStatusAsync(Invoice invoice, CancellationToken cancellationToken = default);
}
