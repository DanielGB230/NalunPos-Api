using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Domain.Enums;

namespace Pos.Infrastructure.ExternalServices.Dummy;

/// <summary>
/// Implementación simulada de la Capa Anti-Corrupción (ACL) para facturación electrónica.
/// Simula respuesta de aceptación del ente tributario (SUNAT/SAT/SII) sin llamar servicios externos.
/// </summary>
public class DummyElectronicInvoicingService : IElectronicInvoicingService
{
    public Task<InvoicingServiceResult> SendInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        string mockHash = $"HASH-CDR-{Guid.NewGuid().ToString()[..12].ToUpperInvariant()}";
        var result = new InvoicingServiceResult(
            Status: ElectronicInvoiceProviderStatus.Accepted,
            DocumentNumber: invoice.DocumentNumber,
            ResponseHash: mockHash,
            ErrorMessage: null
        );

        return Task.FromResult(result);
    }

    public Task<InvoicingServiceResult> GetStatusAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        string mockHash = $"HASH-CDR-{Guid.NewGuid().ToString()[..12].ToUpperInvariant()}";
        var result = new InvoicingServiceResult(
            Status: ElectronicInvoiceProviderStatus.Accepted,
            DocumentNumber: invoice.DocumentNumber,
            ResponseHash: mockHash,
            ErrorMessage: null
        );

        return Task.FromResult(result);
    }
}
