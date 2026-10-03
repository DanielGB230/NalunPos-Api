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

        var (items, totalCount) = await _transferRepository.GetPagedAsync(
            query.PageNumber,
            query.PageSize,
            sourceWarehouseId: query.SourceWarehouseId,
            destinationWarehouseId: query.DestinationWarehouseId,
            cancellationToken: cancellationToken);
        var dtos = items.Select(StockTransferDto.FromEntity).ToList();

        var result = new PagedResult<StockTransferDto>(dtos, query.PageNumber, query.PageSize, totalCount);
        return Result.Ok(result);
    }
}
