using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.CashRegisters.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.CashRegisters.Commands;

[HasPermission(Permissions.CashRegisters.OpenSession)]
public record OpenCashRegisterSessionCommand(
    Guid CashRegisterId,
    Guid UserId,
    decimal InitialAmount,
    string Currency = "USD",
    string? Notes = null
) : ICommand<Result<CashRegisterSessionDto>>;

[HasPermission(Permissions.CashRegisters.OpenSession)]


[HasPermission(Permissions.CashRegisters.OpenSession)]
public class OpenCashRegisterSessionCommandHandler : ICommandHandler<OpenCashRegisterSessionCommand, Result<CashRegisterSessionDto>>
{
    private readonly ICashRegisterRepository _registerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OpenCashRegisterSessionCommandHandler(ICashRegisterRepository registerRepository, IUnitOfWork unitOfWork)
    {
        _registerRepository = registerRepository ?? throw new ArgumentNullException(nameof(registerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<CashRegisterSessionDto>> HandleAsync(OpenCashRegisterSessionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var register = await _registerRepository.GetByIdAsync(request.CashRegisterId, cancellationToken);
        if (register == null)
        {
            return Result.Fail<CashRegisterSessionDto>(DomainError.NotFound(
                "CashRegister.NotFound",
                $"No se encontró la caja registradora con ID '{request.CashRegisterId}'."));
        }

        var activeSession = await _registerRepository.GetActiveSessionByRegisterIdAsync(request.CashRegisterId, cancellationToken);
        if (activeSession != null)
        {
            return Result.Fail<CashRegisterSessionDto>(DomainError.Conflict(
                "CashRegisterSession.AlreadyOpen",
                $"La caja '{register.Name}' ya cuenta con una sesión abierta activa."));
        }

        CashRegisterSession session;
        try
        {
            var initialMoney = Money.Create(request.InitialAmount, request.Currency);
            session = CashRegisterSession.Open(request.CashRegisterId, request.UserId, initialMoney, request.Notes);

            register.SetCurrentSession(session.Id);
        }
        catch (DomainException ex)
        {
            return Result.Fail<CashRegisterSessionDto>(DomainError.Validation("CashRegisterSession.Invalid", ex.Message));
        }

        await _registerRepository.AddSessionAsync(session, cancellationToken);
        _registerRepository.Update(register);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(CashRegisterSessionDto.FromEntity(session));
    }
}
