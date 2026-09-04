using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.PosDevices.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.PosDevices.Commands;

public record RegisterPosDeviceCommand(
    Guid BranchId,
    string Name,
    string SerialNumber
) : ICommand<PosDeviceDto>;

public class RegisterPosDeviceCommandValidator : AbstractValidator<RegisterPosDeviceCommand>
{
    public RegisterPosDeviceCommandValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("El ID de la sucursal es requerido.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del dispositivo es requerido.");

        RuleFor(x => x.SerialNumber)
            .NotEmpty().WithMessage("El número de serie o MAC del dispositivo es requerido.");
    }
}

public class RegisterPosDeviceCommandHandler : ICommandHandler<RegisterPosDeviceCommand, PosDeviceDto>
{
    private readonly IPosDeviceRepository _posDeviceRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterPosDeviceCommandHandler(
        IPosDeviceRepository posDeviceRepository,
        IBranchRepository branchRepository,
        IUnitOfWork unitOfWork)
    {
        _posDeviceRepository = posDeviceRepository ?? throw new ArgumentNullException(nameof(posDeviceRepository));
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<PosDeviceDto> HandleAsync(RegisterPosDeviceCommand request, CancellationToken cancellationToken)
    {
        var branch = await _branchRepository.GetByIdAsync(request.BranchId, cancellationToken)
            ?? throw new BranchNotFoundException(request.BranchId);

        bool serialExists = await _posDeviceRepository.ExistsBySerialNumberAsync(request.SerialNumber, null, cancellationToken);
        if (serialExists)
        {
            throw new DomainException($"Ya existe un dispositivo POS registrado con la serie/MAC '{request.SerialNumber}'.");
        }

        var device = PosDevice.Create(request.BranchId, request.Name, request.SerialNumber);

        await _posDeviceRepository.AddAsync(device, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return PosDeviceDto.FromEntity(device);
    }
}
