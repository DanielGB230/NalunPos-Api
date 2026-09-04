using Pos.Application.Common.Interfaces;
using Pos.Application.Purchases.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Purchases.Queries;

public record GetPurchaseByIdQuery(Guid Id) : IQuery<Result<PurchaseDto>>;

public class GetPurchaseByIdQueryHandler : IQueryHandler<GetPurchaseByIdQuery, Result<PurchaseDto>>
{
    private readonly IPurchaseRepository _purchaseRepository;

    public GetPurchaseByIdQueryHandler(IPurchaseRepository purchaseRepository)
    {
        _purchaseRepository = purchaseRepository ?? throw new ArgumentNullException(nameof(purchaseRepository));
    }

    public async Task<Result<PurchaseDto>> HandleAsync(GetPurchaseByIdQuery request, CancellationToken cancellationToken)
    {
        var purchase = await _purchaseRepository.GetByIdAsync(request.Id, cancellationToken);
        if (purchase == null)
        {
            return Result.Fail<PurchaseDto>(DomainError.NotFound("Purchase.NotFound", $"No se encontró la orden de compra con el ID '{request.Id}'."));
        }

        return Result.Ok(PurchaseDto.FromEntity(purchase));
    }
}
