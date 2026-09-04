using Pos.Application.Common.Interfaces;
using Pos.Application.Payments.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.Payments.Queries;

public record GetPaymentsBySaleIdQuery(Guid SaleId) : IQuery<IReadOnlyList<PaymentDto>>;

public class GetPaymentsBySaleIdQueryHandler : IQueryHandler<GetPaymentsBySaleIdQuery, IReadOnlyList<PaymentDto>>
{
    private readonly IPaymentRepository _paymentRepository;

    public GetPaymentsBySaleIdQueryHandler(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
    }

    public async Task<IReadOnlyList<PaymentDto>> HandleAsync(GetPaymentsBySaleIdQuery request, CancellationToken cancellationToken)
    {
        var items = await _paymentRepository.GetBySaleIdAsync(request.SaleId, cancellationToken);
        return items.Select(PaymentDto.FromEntity).ToList();
    }
}
