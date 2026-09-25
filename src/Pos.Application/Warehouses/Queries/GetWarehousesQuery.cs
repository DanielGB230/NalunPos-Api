using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Warehouses.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Warehouses.Queries;

[HasPermission(Permissions.Warehouses.View)]
public record GetWarehousesQuery : IQuery<Result<IReadOnlyList<WarehouseDto>>>;

public class GetWarehousesQueryHandler : IQueryHandler<GetWarehousesQuery, Result<IReadOnlyList<WarehouseDto>>>
{
    private readonly IWarehouseRepository _warehouseRepository;

    public GetWarehousesQueryHandler(IWarehouseRepository warehouseRepository)
        => _warehouseRepository = warehouseRepository ?? throw new ArgumentNullException(nameof(warehouseRepository));

    public async Task<Result<IReadOnlyList<WarehouseDto>>> HandleAsync(GetWarehousesQuery query, CancellationToken cancellationToken)
    {
        var warehouses = await _warehouseRepository.GetAllAsync(cancellationToken);
        var dtos = warehouses.Select(WarehouseDto.FromEntity).ToList();
        return Result.Ok<IReadOnlyList<WarehouseDto>>(dtos);
    }
}
