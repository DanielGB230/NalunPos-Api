using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.CashRegisters.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;

namespace Pos.Application.CashRegisters.Commands;

[HasPermission(Permissions.CashRegisters.Create)]
public record CreateCashRegisterCommand(string Name, string SerialNumber = "") : ICommand<CashRegisterDto>;

[HasPermission(Permissions.CashRegisters.Create)]


[HasPermission(Permissions.CashRegisters.Create)]
public class CreateCashRegisterCommandHandler : ICommandHandler<CreateCashRegisterCommand, CashRegisterDto>
{
    private readonly ICashRegisterRepository _registerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCashRegisterCommandHandler(ICashRegisterRepository registerRepository, IUnitOfWork unitOfWork)
    {
        _registerRepository = registerRepository ?? throw new ArgumentNullException(nameof(registerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<CashRegisterDto> HandleAsync(CreateCashRegisterCommand request, CancellationToken cancellationToken)
    {
        var register = CashRegister.Create(request.Name, request.SerialNumber);

        await _registerRepository.AddAsync(register, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CashRegisterDto.FromEntity(register);
    }
}
