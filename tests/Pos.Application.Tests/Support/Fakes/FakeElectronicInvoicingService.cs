namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Domain.Enums;

public sealed class FakeElectronicInvoicingService : IElectronicInvoicingService
{
    public ElectronicInvoiceProviderStatus? StatusToReturn { get; set; }
    public ElectronicInvoiceProviderStatus? SendStatusToReturn { get; set; }

    public void ReturnStatus(ElectronicInvoiceProviderStatus status)
    {
        StatusToReturn = status;
    }

    public void ReturnSendStatus(ElectronicInvoiceProviderStatus status)
    {
        SendStatusToReturn = status;
    }

    public Task<InvoicingServiceResult> GetStatusAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        if (StatusToReturn is null)
        {
            throw new InvalidOperationException("FakeElectronicInvoicingService no configurado: llama ReturnStatus()");
        }

        return Task.FromResult(new InvoicingServiceResult(StatusToReturn.Value, invoice.DocumentNumber, "HASH", null));
    }

    public Task<InvoicingServiceResult> SendInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        if (SendStatusToReturn is null)
        {
            throw new InvalidOperationException("FakeElectronicInvoicingService no configurado: llama ReturnSendStatus()");
        }

        return Task.FromResult(new InvoicingServiceResult(SendStatusToReturn.Value, invoice.DocumentNumber, "HASH", null));
    }
}
