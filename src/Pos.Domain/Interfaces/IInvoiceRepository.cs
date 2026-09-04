using Pos.Domain.Entities;

namespace Pos.Domain.Interfaces;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Invoice?> GetBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default);
    Task<Invoice?> GetByDocumentNumberAsync(string documentNumber, CancellationToken cancellationToken = default);
    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);
    void Update(Invoice invoice);
}
