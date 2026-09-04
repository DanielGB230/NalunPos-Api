using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Purchases.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.Purchases.Queries;

public record GetPurchasesQuery(
    int PageNumber = 1,
    int PageSize = 10,
    Guid? SupplierId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null
) : IQuery<PagedResult<PurchaseDto>>;

public class GetPurchasesQueryHandler : IQueryHandler<GetPurchasesQuery, PagedResult<PurchaseDto>>
{
    private readonly IPurchaseRepository _purchaseRepository;

    public GetPurchasesQueryHandler(IPurchaseRepository purchaseRepository)
    {
        _purchaseRepository = purchaseRepository ?? throw new ArgumentNullException(nameof(purchaseRepository));
    }

    public async Task<PagedResult<PurchaseDto>> HandleAsync(GetPurchasesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _purchaseRepository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            request.SupplierId,
            request.StartDate,
            request.EndDate,
            cancellationToken);

        var dtos = items.Select(PurchaseDto.FromEntity).ToList();

        return new PagedResult<PurchaseDto>(dtos, request.PageNumber, request.PageSize, totalCount);
    }
}
