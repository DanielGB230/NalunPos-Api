using Pos.Application.Common.Interfaces;
using Pos.Application.PurchaseOrders.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.PurchaseOrders.Queries;

public record GetPurchaseOrdersQuery(int PageNumber = 1, int PageSize = 20)
    : IQuery<Result<(IReadOnlyList<PurchaseOrderDto> Items, int TotalCount)>>;

public class GetPurchaseOrdersQueryHandler
    : IQueryHandler<GetPurchaseOrdersQuery, Result<(IReadOnlyList<PurchaseOrderDto> Items, int TotalCount)>>
{
    private readonly IPurchaseOrderRepository _orderRepository;

    public GetPurchaseOrdersQueryHandler(IPurchaseOrderRepository orderRepository)
        => _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));

    public async Task<Result<(IReadOnlyList<PurchaseOrderDto> Items, int TotalCount)>> HandleAsync(
        GetPurchaseOrdersQuery query, CancellationToken cancellationToken)
    {
        var (items, total) = await _orderRepository.GetPagedAsync(query.PageNumber, query.PageSize, status: null, cancellationToken);
        var dtos = items.Select(PurchaseOrderDto.FromEntity).ToList();
        return Result.Ok<(IReadOnlyList<PurchaseOrderDto>, int)>((dtos, total));
    }
}
