using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.CashRegisters.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.CashRegisters.Commands;

public record CloseCashRegisterSessionCommand(
    Guid SessionId,
    decimal ActualFinalAmount,
    decimal ExpectedFinalAmount,
    string Currency = "USD",
    string? Notes = null
) : ICommand<CashRegisterSessionDto>;

public class CloseCashRegisterSessionCommandValidator : AbstractValidator<CloseCashRegisterSessionCommand>
{
    public CloseCashRegisterSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("El ID de la sesión de caja es requerido.");

        RuleFor(x => x.ActualFinalAmount)
            .GreaterThanOrEqualTo(0).WithMessage("El monto final declarado no puede ser negativo.");

        RuleFor(x => x.ExpectedFinalAmount)
            .GreaterThanOrEqualTo(0).WithMessage("El monto final esperado no puede ser negativo.");
    }
}

public class CloseCashRegisterSessionCommandHandler : ICommandHandler<CloseCashRegisterSessionCommand, CashRegisterSessionDto>
{
    private readonly ICashRegisterRepository _registerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CloseCashRegisterSessionCommandHandler(ICashRegisterRepository registerRepository, IUnitOfWork unitOfWork)
    {
        _registerRepository = registerRepository ?? throw new ArgumentNullException(nameof(registerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<CashRegisterSessionDto> HandleAsync(CloseCashRegisterSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _registerRepository.GetSessionByIdAsync(request.SessionId, cancellationToken)
            ?? throw new CashRegisterSessionNotFoundException(request.SessionId);

        var register = await _registerRepository.GetByIdAsync(session.CashRegisterId, cancellationToken)
            ?? throw new CashRegisterNotFoundException(session.CashRegisterId);

        var actualMoney = Money.Create(request.ActualFinalAmount, request.Currency);
        var expectedMoney = Money.Create(request.ExpectedFinalAmount, request.Currency);

        session.Close(actualMoney, expectedMoney, request.Notes);
        register.ClearCurrentSession();

        _registerRepository.UpdateSession(session);
        _registerRepository.Update(register);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CashRegisterSessionDto.FromEntity(session);
    }
}
