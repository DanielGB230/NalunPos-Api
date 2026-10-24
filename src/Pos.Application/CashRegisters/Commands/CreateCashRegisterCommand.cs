using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.CashRegisters.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.CashRegisters.Commands;

[RequiresTenant]
[HasPermission(Permissions.CashRegisters.Create)]
public record CreateCashRegisterCommand(
    Guid BranchId,
    string Name,
    string SerialNumber = ""
) : ICommand<Result<CashRegisterDto>>;

public class CreateCashRegisterCommandHandler : ICommandHandler<CreateCashRegisterCommand, Result<CashRegisterDto>>
{
    private readonly ICashRegisterRepository _registerRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public CreateCashRegisterCommandHandler(
        ICashRegisterRepository registerRepository,
        IBranchRepository branchRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _registerRepository = registerRepository ?? throw new ArgumentNullException(nameof(registerRepository));
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Result<CashRegisterDto>> HandleAsync(CreateCashRegisterCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var branch = await _branchRepository.GetByIdAsync(request.BranchId, cancellationToken);
        if (branch == null || branch.TenantId != tenantId)
        {
            return Result.Fail<CashRegisterDto>(DomainError.NotFound("Branch.NotFound", $"No se encontró la sucursal con el ID '{request.BranchId}'."));
        }

        CashRegister register;
        try
        {
            register = CashRegister.Create(tenantId, request.BranchId, request.Name, request.SerialNumber, _timeProvider);
        }
        catch (DomainException ex)
        {
            return Result.Fail<CashRegisterDto>(DomainError.Validation("CashRegister.Invalid", ex.Message));
        }

        await _registerRepository.AddAsync(register, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(CashRegisterDto.FromEntity(register));
    }
}

