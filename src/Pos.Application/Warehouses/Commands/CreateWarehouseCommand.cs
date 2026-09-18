using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.Warehouses.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Warehouses.Commands;

public record CreateWarehouseCommand(
    Guid BranchId,
    string Name,
    string? Description = null,
    bool IsDefault = false
) : ICommand<Result<WarehouseDto>>;

public class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("El ID de la sucursal es requerido.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithMessage("El nombre del almacén es requerido (máx. 100 caracteres).");
    }
}

public class CreateWarehouseCommandHandler : ICommandHandler<CreateWarehouseCommand, Result<WarehouseDto>>
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;

    public CreateWarehouseCommandHandler(
        IWarehouseRepository warehouseRepository,
        IBranchRepository branchRepository,
        ICurrentTenantContext tenantContext,
        IUnitOfWork unitOfWork)
    {
        _warehouseRepository = warehouseRepository ?? throw new ArgumentNullException(nameof(warehouseRepository));
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<WarehouseDto>> HandleAsync(CreateWarehouseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var branch = await _branchRepository.GetByIdAsync(command.BranchId, cancellationToken);
        if (branch is null)
            return Result.Fail<WarehouseDto>(DomainError.NotFound("Branch.NotFound", $"No se encontró la sucursal con ID '{command.BranchId}'."));

        Guid tenantId = _tenantContext.TenantId ?? Guid.Empty;

        // Si se solicita IsDefault, desasignar el default actual
        if (command.IsDefault)
        {
            var currentDefault = await _warehouseRepository.GetDefaultAsync(cancellationToken);
            if (currentDefault is not null)
                currentDefault.UnsetDefault();
        }

        Warehouse warehouse;
        try
        {
            warehouse = Warehouse.Create(tenantId, command.BranchId, command.Name, command.Description, command.IsDefault);
        }
        catch (DomainException ex)
        {
            return Result.Fail<WarehouseDto>(DomainError.Validation("Warehouse.Invalid", ex.Message));
        }

        await _warehouseRepository.AddAsync(warehouse, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(WarehouseDto.FromEntity(warehouse));
    }
}
