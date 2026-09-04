using Pos.Application.Common.Interfaces;
using Pos.Application.PosDevices.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.PosDevices.Queries;

public record GetPosDeviceByIdQuery(Guid Id) : IQuery<PosDeviceDto>;

public class GetPosDeviceByIdQueryHandler : IQueryHandler<GetPosDeviceByIdQuery, PosDeviceDto>
{
    private readonly IPosDeviceRepository _posDeviceRepository;

    public GetPosDeviceByIdQueryHandler(IPosDeviceRepository posDeviceRepository)
    {
        _posDeviceRepository = posDeviceRepository ?? throw new ArgumentNullException(nameof(posDeviceRepository));
    }

    public async Task<PosDeviceDto> HandleAsync(GetPosDeviceByIdQuery request, CancellationToken cancellationToken)
    {
        var device = await _posDeviceRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new PosDeviceNotFoundException(request.Id);

        return PosDeviceDto.FromEntity(device);
    }
}
