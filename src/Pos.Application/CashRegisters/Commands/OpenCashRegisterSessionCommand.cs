using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.CashRegisters.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.CashRegisters.Commands;

public record OpenCashRegisterSessionCommand(
    Guid CashRegisterId,
    Guid UserId,
    decimal InitialAmount,
    string Currency = "USD",
    string? Notes = null
) : ICommand<CashRegisterSessionDto>;

public class OpenCashRegisterSessionCommandValidator : AbstractValidator<OpenCashRegisterSessionCommand>
{
    public OpenCashRegisterSessionCommandValidator()
    {
        RuleFor(x => x.CashRegisterId)
            .NotEmpty().WithMessage("El ID de la caja es requerido.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("El ID del usuario/cajero es requerido.");

        RuleFor(x => x.InitialAmount)
            .GreaterThanOrEqualTo(0).WithMessage("El monto inicial no puede ser negativo.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("La divisa es requerida.")
            .Length(3).WithMessage("La divisa debe ser de 3 caracteres.");
    }
}

public class OpenCashRegisterSessionCommandHandler : ICommandHandler<OpenCashRegisterSessionCommand, CashRegisterSessionDto>
{
    private readonly ICashRegisterRepository _registerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OpenCashRegisterSessionCommandHandler(ICashRegisterRepository registerRepository, IUnitOfWork unitOfWork)
    {
        _registerRepository = registerRepository ?? throw new ArgumentNullException(nameof(registerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<CashRegisterSessionDto> HandleAsync(OpenCashRegisterSessionCommand request, CancellationToken cancellationToken)
    {
        var register = await _registerRepository.GetByIdAsync(request.CashRegisterId, cancellationToken)
            ?? throw new CashRegisterNotFoundException(request.CashRegisterId);

        var activeSession = await _registerRepository.GetActiveSessionByRegisterIdAsync(request.CashRegisterId, cancellationToken);
        if (activeSession != null)
        {
            throw new DomainException($"La caja '{register.Name}' ya cuenta con una sesión abierta (ID: {activeSession.Id}).");
        }

        var initialMoney = Money.Create(request.InitialAmount, request.Currency);
        var session = CashRegisterSession.Open(request.CashRegisterId, request.UserId, initialMoney, request.Notes);

        register.SetCurrentSession(session.Id);

        await _registerRepository.AddSessionAsync(session, cancellationToken);
        _registerRepository.Update(register);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CashRegisterSessionDto.FromEntity(session);
    }
}
