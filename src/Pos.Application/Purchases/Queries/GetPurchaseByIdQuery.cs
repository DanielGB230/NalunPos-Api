using Pos.Application.Common.Interfaces;
using Pos.Application.Purchases.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Purchases.Queries;

public record GetPurchaseByIdQuery(Guid Id) : IQuery<PurchaseDto>;

public class GetPurchaseByIdQueryHandler : IQueryHandler<GetPurchaseByIdQuery, PurchaseDto>
{
    private readonly IPurchaseRepository _purchaseRepository;

    public GetPurchaseByIdQueryHandler(IPurchaseRepository purchaseRepository)
    {
        _purchaseRepository = purchaseRepository ?? throw new ArgumentNullException(nameof(purchaseRepository));
    }

    public async Task<PurchaseDto> HandleAsync(GetPurchaseByIdQuery request, CancellationToken cancellationToken)
    {
        var purchase = await _purchaseRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new PurchaseNotFoundException(request.Id);

        return PurchaseDto.FromEntity(purchase);
    }
}
