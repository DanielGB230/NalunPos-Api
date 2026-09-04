using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Sales.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.Sales.Queries;

public record GetSalesQuery(
    int PageNumber = 1,
    int PageSize = 10,
    Guid? SessionId = null,
    Guid? CustomerId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null
) : IQuery<PagedResult<SaleDto>>;

public class GetSalesQueryHandler : IQueryHandler<GetSalesQuery, PagedResult<SaleDto>>
{
    private readonly ISaleRepository _saleRepository;

    public GetSalesQueryHandler(ISaleRepository saleRepository)
    {
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
    }

    public async Task<PagedResult<SaleDto>> HandleAsync(GetSalesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _saleRepository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            request.SessionId,
            request.CustomerId,
            request.StartDate,
            request.EndDate,
            cancellationToken);

        var dtos = items.Select(SaleDto.FromEntity).ToList();

        return new PagedResult<SaleDto>(dtos, request.PageNumber, request.PageSize, totalCount);
    }
}
