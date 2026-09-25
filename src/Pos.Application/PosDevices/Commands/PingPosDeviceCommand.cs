using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.PosDevices.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.PosDevices.Commands;

[HasPermission(Permissions.PosDevices.Ping)]
public record PingPosDeviceCommand(Guid Id) : ICommand<Result<PosDeviceDto>>;

[HasPermission(Permissions.PosDevices.Ping)]
public class PingPosDeviceCommandHandler : ICommandHandler<PingPosDeviceCommand, Result<PosDeviceDto>>
{
    private readonly IPosDeviceRepository _posDeviceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PingPosDeviceCommandHandler(IPosDeviceRepository posDeviceRepository, IUnitOfWork unitOfWork)
    {
        _posDeviceRepository = posDeviceRepository ?? throw new ArgumentNullException(nameof(posDeviceRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<PosDeviceDto>> HandleAsync(PingPosDeviceCommand request, CancellationToken cancellationToken)
    {
        var device = await _posDeviceRepository.GetByIdAsync(request.Id, cancellationToken);
        if (device == null)
        {
            return Result.Fail<PosDeviceDto>(DomainError.NotFound("PosDevice.NotFound", $"No se encontró el dispositivo POS con el ID '{request.Id}'."));
        }

        device.RecordPing();

        _posDeviceRepository.Update(device);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(PosDeviceDto.FromEntity(device));
    }
}
