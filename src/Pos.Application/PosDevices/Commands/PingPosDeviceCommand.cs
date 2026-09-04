using Pos.Application.Common.Interfaces;
using Pos.Application.PosDevices.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.PosDevices.Commands;

public record PingPosDeviceCommand(Guid Id) : ICommand<PosDeviceDto>;

public class PingPosDeviceCommandHandler : ICommandHandler<PingPosDeviceCommand, PosDeviceDto>
{
    private readonly IPosDeviceRepository _posDeviceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PingPosDeviceCommandHandler(IPosDeviceRepository posDeviceRepository, IUnitOfWork unitOfWork)
    {
        _posDeviceRepository = posDeviceRepository ?? throw new ArgumentNullException(nameof(posDeviceRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<PosDeviceDto> HandleAsync(PingPosDeviceCommand request, CancellationToken cancellationToken)
    {
        var device = await _posDeviceRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new PosDeviceNotFoundException(request.Id);

        device.RecordPing();

        _posDeviceRepository.Update(device);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return PosDeviceDto.FromEntity(device);
    }
}
