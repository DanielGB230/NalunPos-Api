namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Domain.Enums;

public sealed class FakeElectronicInvoicingService : IElectronicInvoicingService
{
    /// <summary>Estado de consulta que devolverá el proveedor electrónico.</summary>
    public ElectronicInvoiceProviderStatus? StatusToReturn { get; private set; }
    /// <summary>Estado de envío que devolverá el proveedor electrónico.</summary>
    public ElectronicInvoiceProviderStatus? SendStatusToReturn { get; private set; }

    /// <summary>Configura el estado a retornar en consultas de comprobantes.</summary>
    public void ReturnStatus(ElectronicInvoiceProviderStatus status)
    {
        StatusToReturn = status;
    }

    /// <summary>Contador de llamadas a GetStatusAsync.</summary>
    public int GetStatusCallCount { get; private set; }
    /// <summary>Contador de llamadas a SendInvoiceAsync.</summary>
    public int SendInvoiceCallCount { get; private set; }

    /// <summary>Configura el estado a retornar al enviar comprobantes.</summary>
    public void ReturnSendStatus(ElectronicInvoiceProviderStatus status)
    {
        SendStatusToReturn = status;
    }

    public Task<InvoicingServiceResult> GetStatusAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        GetStatusCallCount++;
        if (StatusToReturn is null)
        {
            throw new InvalidOperationException("FakeElectronicInvoicingService no configurado: llama ReturnStatus()");
        }

        return Task.FromResult(new InvoicingServiceResult(StatusToReturn.Value, invoice.DocumentNumber, "HASH", null));
    }

    public Task<InvoicingServiceResult> SendInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        SendInvoiceCallCount++;
        if (SendStatusToReturn is null)
        {
            throw new InvalidOperationException("FakeElectronicInvoicingService no configurado: llama ReturnSendStatus()");
        }

        return Task.FromResult(new InvoicingServiceResult(SendStatusToReturn.Value, invoice.DocumentNumber, "HASH", null));
    }
}
