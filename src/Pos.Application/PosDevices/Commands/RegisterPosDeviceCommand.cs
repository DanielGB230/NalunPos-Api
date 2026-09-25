using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.PosDevices.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

using Pos.Domain.Common;

namespace Pos.Application.PosDevices.Commands;

[HasPermission(Permissions.PosDevices.Register)]
public record RegisterPosDeviceCommand(
    Guid BranchId,
    string Name,
    string SerialNumber
) : ICommand<Result<PosDeviceDto>>;

[HasPermission(Permissions.PosDevices.Register)]
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

[HasPermission(Permissions.PosDevices.Register)]
public class RegisterPosDeviceCommandHandler : ICommandHandler<RegisterPosDeviceCommand, Result<PosDeviceDto>>
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

    public async Task<Result<PosDeviceDto>> HandleAsync(RegisterPosDeviceCommand request, CancellationToken cancellationToken)
    {
        var branch = await _branchRepository.GetByIdAsync(request.BranchId, cancellationToken);
        if (branch == null)
        {
            return Result.Fail<PosDeviceDto>(DomainError.NotFound("Branch.NotFound", $"No se encontró la sucursal con el ID '{request.BranchId}'."));
        }

        bool serialExists = await _posDeviceRepository.ExistsBySerialNumberAsync(request.SerialNumber, null, cancellationToken);
        if (serialExists)
        {
            return Result.Fail<PosDeviceDto>(DomainError.Conflict("PosDevice.AlreadyExists", $"Ya existe un dispositivo POS registrado con la serie/MAC '{request.SerialNumber}'."));
        }

        PosDevice device;
        try
        {
            device = PosDevice.Create(request.BranchId, request.Name, request.SerialNumber);
        }
        catch (DomainException ex)
        {
            return Result.Fail<PosDeviceDto>(DomainError.Validation("PosDevice.Invalid", ex.Message));
        }

        await _posDeviceRepository.AddAsync(device, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(PosDeviceDto.FromEntity(device));
    }
}
