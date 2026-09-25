using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Sales.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.Sales.Queries;

[HasPermission(Permissions.Sales.View)]
public record GetSalesQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? CustomerId = null,
    Pos.Domain.Enums.SaleStatus? Status = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null
) : IQuery<PagedResult<SaleDto>>;

[HasPermission(Permissions.Sales.View)]
public class GetSalesQueryHandler : IQueryHandler<GetSalesQuery, PagedResult<SaleDto>>
{
    private readonly ISaleRepository _saleRepository;

    public GetSalesQueryHandler(ISaleRepository saleRepository)
    {
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
    }

    public async Task<PagedResult<SaleDto>> HandleAsync(GetSalesQuery request, CancellationToken cancellationToken)
    {
        int pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        int pageSize = Math.Min(request.PageSize < 1 ? 20 : request.PageSize, 100);

        var (items, totalCount) = await _saleRepository.GetPagedAsync(
            pageNumber,
            pageSize,
            null, // SessionId
            request.CustomerId,
            request.StartDate,
            request.EndDate,
            cancellationToken);

        var dtos = items.Select(SaleDto.FromEntity).ToList();

        return new PagedResult<SaleDto>(dtos, pageNumber, pageSize, totalCount);
    }
}
