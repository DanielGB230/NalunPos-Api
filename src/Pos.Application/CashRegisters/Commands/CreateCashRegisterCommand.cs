using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.CashRegisters.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;

namespace Pos.Application.CashRegisters.Commands;

public record CreateCashRegisterCommand(string Name, string SerialNumber = "") : ICommand<CashRegisterDto>;

public class CreateCashRegisterCommandValidator : AbstractValidator<CreateCashRegisterCommand>
{
    public CreateCashRegisterCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la caja es requerido.")
            .Length(2, 100).WithMessage("El nombre debe contener entre 2 y 100 caracteres.");
    }
}

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
