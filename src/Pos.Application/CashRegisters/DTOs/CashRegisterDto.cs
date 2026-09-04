using Pos.Domain.Entities;

namespace Pos.Application.CashRegisters.DTOs;

public record CashRegisterDto(
    Guid Id,
    string Name,
    string SerialNumber,
    bool IsActive,
    Guid? CurrentSessionId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
)
{
    public static CashRegisterDto FromEntity(CashRegister register)
    {
        return new CashRegisterDto(
            register.Id,
            register.Name,
            register.SerialNumber,
            register.IsActive,
            register.CurrentSessionId,
            register.CreatedAtUtc,
            register.UpdatedAtUtc
        );
    }
}

public record CashRegisterSessionDto(
    Guid Id,
    Guid CashRegisterId,
    Guid UserId,
    DateTime OpenedAtUtc,
    DateTime? ClosedAtUtc,
    decimal InitialAmount,
    string Currency,
    decimal? ExpectedFinalAmount,
    decimal? ActualFinalAmount,
    string StatusName,
    string? Notes
)
{
    public static CashRegisterSessionDto FromEntity(CashRegisterSession session)
    {
        return new CashRegisterSessionDto(
            session.Id,
            session.CashRegisterId,
            session.UserId,
            session.OpenedAtUtc,
            session.ClosedAtUtc,
            session.InitialAmount.Amount,
            session.InitialAmount.Currency,
            session.ExpectedFinalAmount?.Amount,
            session.ActualFinalAmount?.Amount,
            session.Status.ToString(),
            session.Notes
        );
    }
}
