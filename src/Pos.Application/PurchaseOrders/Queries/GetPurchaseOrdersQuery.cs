using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.PurchaseOrders.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.PurchaseOrders.Queries;

[HasPermission(Permissions.PurchaseOrders.View)]
public record GetPurchaseOrdersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? SupplierId = null,
    Guid? WarehouseId = null,
    Pos.Domain.Enums.PurchaseOrderStatus? Status = null
) : IQuery<Result<(IReadOnlyList<PurchaseOrderDto> Items, int TotalCount)>>;

[HasPermission(Permissions.PurchaseOrders.View)]
public class GetPurchaseOrdersQueryHandler
    : IQueryHandler<GetPurchaseOrdersQuery, Result<(IReadOnlyList<PurchaseOrderDto> Items, int TotalCount)>>
{
    private readonly IPurchaseOrderRepository _orderRepository;

    public GetPurchaseOrdersQueryHandler(IPurchaseOrderRepository orderRepository)
        => _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));

    public async Task<Result<(IReadOnlyList<PurchaseOrderDto> Items, int TotalCount)>> HandleAsync(
        GetPurchaseOrdersQuery query, CancellationToken cancellationToken)
    {
        int pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        int pageSize = Math.Min(query.PageSize < 1 ? 20 : query.PageSize, 100);

        var (items, total) = await _orderRepository.GetPagedAsync(
            pageNumber,
            pageSize,
            status: query.Status,
            supplierId: query.SupplierId,
            warehouseId: query.WarehouseId,
            cancellationToken: cancellationToken);
        var dtos = items.Select(PurchaseOrderDto.FromEntity).ToList();
        return Result.Ok<(IReadOnlyList<PurchaseOrderDto>, int)>((dtos, total));
    }
}
