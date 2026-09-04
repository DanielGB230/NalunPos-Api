using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado para Turno / Sesión de Caja (Apertura y Cierre).
/// </summary>
public class CashRegisterSession : AggregateRoot<Guid>
{
    public Guid CashRegisterId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime OpenedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public Money InitialAmount { get; private set; } = null!;
    public Money? ExpectedFinalAmount { get; private set; }
    public Money? ActualFinalAmount { get; private set; }
    public SessionStatus Status { get; private set; }
    public string? Notes { get; private set; }

    private CashRegisterSession()
    {
    }

    private CashRegisterSession(
        Guid id,
        Guid cashRegisterId,
        Guid userId,
        Money initialAmount,
        string? notes) : base(id)
    {
        if (cashRegisterId == Guid.Empty)
        {
            throw new DomainException("El ID de la caja es requerido para abrir un turno.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainException("El ID del usuario/cajero es requerido.");
        }

        CashRegisterId = cashRegisterId;
        UserId = userId;
        InitialAmount = initialAmount ?? throw new ArgumentNullException(nameof(initialAmount));
        Status = SessionStatus.Open;
        OpenedAtUtc = DateTime.UtcNow;
        Notes = notes?.Trim();

        RaiseDomainEvent(new CashRegisterSessionOpenedDomainEvent(
            Id,
            CashRegisterId,
            UserId,
            InitialAmount.Amount,
            InitialAmount.Currency,
            OpenedAtUtc));
    }

    public static CashRegisterSession Open(
        Guid cashRegisterId,
        Guid userId,
        Money initialAmount,
        string? notes = null)
    {
        return new CashRegisterSession(Guid.NewGuid(), cashRegisterId, userId, initialAmount, notes);
    }

    public void Close(Money actualFinalAmount, Money expectedFinalAmount, string? notes = null)
    {
        if (Status == SessionStatus.Closed)
        {
            throw new DomainException("La sesión de caja ya se encuentra cerrada.");
        }

        ActualFinalAmount = actualFinalAmount ?? throw new ArgumentNullException(nameof(actualFinalAmount));
        ExpectedFinalAmount = expectedFinalAmount ?? throw new ArgumentNullException(nameof(expectedFinalAmount));
        Status = SessionStatus.Closed;
        ClosedAtUtc = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(notes))
        {
            Notes = string.IsNullOrWhiteSpace(Notes) ? notes.Trim() : $"{Notes} | {notes.Trim()}";
        }

        RaiseDomainEvent(new CashRegisterSessionClosedDomainEvent(
            Id,
            CashRegisterId,
            ExpectedFinalAmount.Amount,
            ActualFinalAmount.Amount,
            ActualFinalAmount.Currency,
            ClosedAtUtc.Value));
    }
}
