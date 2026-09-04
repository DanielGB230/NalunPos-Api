using Pos.Application.Common.Interfaces;
using Pos.Application.PosDevices.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.PosDevices.Queries;

public record GetPosDeviceByIdQuery(Guid Id) : IQuery<Result<PosDeviceDto>>;

public class GetPosDeviceByIdQueryHandler : IQueryHandler<GetPosDeviceByIdQuery, Result<PosDeviceDto>>
{
    private readonly IPosDeviceRepository _posDeviceRepository;

    public GetPosDeviceByIdQueryHandler(IPosDeviceRepository posDeviceRepository)
    {
        _posDeviceRepository = posDeviceRepository ?? throw new ArgumentNullException(nameof(posDeviceRepository));
    }

    public async Task<Result<PosDeviceDto>> HandleAsync(GetPosDeviceByIdQuery request, CancellationToken cancellationToken)
    {
        var device = await _posDeviceRepository.GetByIdAsync(request.Id, cancellationToken);
        if (device == null)
        {
            return Result.Fail<PosDeviceDto>(DomainError.NotFound("PosDevice.NotFound", $"No se encontró el dispositivo POS con el ID '{request.Id}'."));
        }

        return Result.Ok(PosDeviceDto.FromEntity(device));
    }
}
