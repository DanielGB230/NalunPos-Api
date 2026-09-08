using Pos.Application.Common.Interfaces;
using Pos.Domain.DomainEvents;
using Pos.Domain.Interfaces;

namespace Pos.Application.Sales.EventHandlers;

/// <summary>
/// Domain Event Handler que escucha la confirmación de un Pago (PaymentProcessedDomainEvent)
/// y desacopladamente marca el estado de la Venta como Paid (Pagada).
/// Mantiene la separación estricta de módulos según principios de Clean Architecture.
/// </summary>
public sealed class MarkSalePaidOnPaymentProcessedHandler
    : IDomainEventHandler<PaymentProcessedDomainEvent>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MarkSalePaidOnPaymentProcessedHandler(
        ISaleRepository saleRepository,
        IUnitOfWork unitOfWork)
    {
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task HandleAsync(PaymentProcessedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var sale = await _saleRepository.GetByIdAsync(domainEvent.SaleId, cancellationToken);
        if (sale is null)
        {
            return;
        }

        sale.MarkAsPaid();

        _saleRepository.Update(sale);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
