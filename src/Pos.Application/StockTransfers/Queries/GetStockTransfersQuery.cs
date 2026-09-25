using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.StockTransfers.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.StockTransfers.Queries;

[HasPermission(Permissions.StockTransfers.View)]
public record GetStockTransfersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? SourceWarehouseId = null,
    Guid? DestinationWarehouseId = null
) : IQuery<Result<PagedResult<StockTransferDto>>>;

[HasPermission(Permissions.StockTransfers.View)]
public class GetStockTransfersQueryHandler : IQueryHandler<GetStockTransfersQuery, Result<PagedResult<StockTransferDto>>>
{
    private readonly IStockTransferRepository _transferRepository;

    public GetStockTransfersQueryHandler(IStockTransferRepository transferRepository)
    {
        _transferRepository = transferRepository ?? throw new ArgumentNullException(nameof(transferRepository));
    }

    public async Task<Result<PagedResult<StockTransferDto>>> HandleAsync(GetStockTransfersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        int page = query.PageNumber < 1 ? 1 : query.PageNumber;
        int size = Math.Min(query.PageSize < 1 ? 20 : query.PageSize, 100);

        var (items, totalCount) = await _transferRepository.GetPagedAsync(
            page,
            size,
            sourceWarehouseId: query.SourceWarehouseId,
            destinationWarehouseId: query.DestinationWarehouseId,
            cancellationToken: cancellationToken);
        var dtos = items.Select(StockTransferDto.FromEntity).ToList();

        var result = new PagedResult<StockTransferDto>(dtos, page, size, totalCount);
        return Result.Ok(result);
    }
}
