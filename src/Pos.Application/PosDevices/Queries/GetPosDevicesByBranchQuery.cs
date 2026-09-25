using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.PosDevices.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.PosDevices.Queries;

[HasPermission(Permissions.PosDevices.View)]
public record GetPosDevicesByBranchQuery(Guid BranchId) : IQuery<IReadOnlyList<PosDeviceDto>>;

[HasPermission(Permissions.PosDevices.View)]
public class GetPosDevicesByBranchQueryHandler : IQueryHandler<GetPosDevicesByBranchQuery, IReadOnlyList<PosDeviceDto>>
{
    private readonly IPosDeviceRepository _posDeviceRepository;

    public GetPosDevicesByBranchQueryHandler(IPosDeviceRepository posDeviceRepository)
    {
        _posDeviceRepository = posDeviceRepository ?? throw new ArgumentNullException(nameof(posDeviceRepository));
    }

    public async Task<IReadOnlyList<PosDeviceDto>> HandleAsync(GetPosDevicesByBranchQuery request, CancellationToken cancellationToken)
    {
        var items = await _posDeviceRepository.GetByBranchIdAsync(request.BranchId, cancellationToken);
        return items.Select(PosDeviceDto.FromEntity).ToList();
    }
}
