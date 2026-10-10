namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Domain.Enums;

public sealed class FakeElectronicInvoicingService : IElectronicInvoicingService
{
    public ElectronicInvoiceProviderStatus StatusToReturn { get; set; } = ElectronicInvoiceProviderStatus.Accepted;
    public ElectronicInvoiceProviderStatus SendStatusToReturn { get; set; } = ElectronicInvoiceProviderStatus.Accepted;

    public Task<InvoicingServiceResult> GetStatusAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new InvoicingServiceResult(StatusToReturn, invoice.DocumentNumber, "HASH", null));
    }

    public Task<InvoicingServiceResult> SendInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new InvoicingServiceResult(SendStatusToReturn, invoice.DocumentNumber, "HASH", null));
    }
}
