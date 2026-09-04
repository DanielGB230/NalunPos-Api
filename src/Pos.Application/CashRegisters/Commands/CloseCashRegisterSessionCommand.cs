using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.CashRegisters.DTOs;
using Pos.Domain.Common;
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
) : ICommand<Result<CashRegisterSessionDto>>;

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

public class CloseCashRegisterSessionCommandHandler : ICommandHandler<CloseCashRegisterSessionCommand, Result<CashRegisterSessionDto>>
{
    private readonly ICashRegisterRepository _registerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CloseCashRegisterSessionCommandHandler(ICashRegisterRepository registerRepository, IUnitOfWork unitOfWork)
    {
        _registerRepository = registerRepository ?? throw new ArgumentNullException(nameof(registerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<CashRegisterSessionDto>> HandleAsync(CloseCashRegisterSessionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await _registerRepository.GetSessionByIdAsync(request.SessionId, cancellationToken);
        if (session == null)
        {
            return Result.Fail<CashRegisterSessionDto>(DomainError.NotFound(
                "CashRegisterSession.NotFound",
                $"No se encontró la sesión de caja con ID '{request.SessionId}'."));
        }

        var register = await _registerRepository.GetByIdAsync(session.CashRegisterId, cancellationToken);
        if (register == null)
        {
            return Result.Fail<CashRegisterSessionDto>(DomainError.NotFound(
                "CashRegister.NotFound",
                $"No se encontró la caja registradora asociada con ID '{session.CashRegisterId}'."));
        }

        try
        {
            var actualMoney = Money.Create(request.ActualFinalAmount, request.Currency);
            var expectedMoney = Money.Create(request.ExpectedFinalAmount, request.Currency);

            session.Close(actualMoney, expectedMoney, request.Notes);
            register.ClearCurrentSession();
        }
        catch (DomainException ex)
        {
            return Result.Fail<CashRegisterSessionDto>(DomainError.Validation("CashRegisterSession.Invalid", ex.Message));
        }

        _registerRepository.UpdateSession(session);
        _registerRepository.Update(register);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(CashRegisterSessionDto.FromEntity(session));
    }
}
