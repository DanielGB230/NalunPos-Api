namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;

/// <summary>
/// Fake estricto de <see cref="IUserSessionRevoker"/>: registra cada revocación,
/// falla ante argumentos inválidos y permite inyectar una excepción o un callback
/// para verificar el orden guardar/revocar (F1.G.7, ADR 0034).
/// </summary>
public sealed class FakeUserSessionRevoker : IUserSessionRevoker
{
    /// <summary>Revocaciones solicitadas, en orden de invocación.</summary>
    public List<(Guid UserId, string Reason)> Calls { get; } = [];

    /// <summary>Cantidad de invocaciones registradas.</summary>
    public int CallCount => Calls.Count;

    /// <summary>Último usuario revocado, si hubo alguna invocación.</summary>
    public Guid? LastUserId => Calls.Count == 0 ? null : Calls[^1].UserId;

    /// <summary>Último motivo de revocación, si hubo alguna invocación.</summary>
    public string? LastReason => Calls.Count == 0 ? null : Calls[^1].Reason;

    /// <summary>Excepción que se lanzará después de registrar la invocación.</summary>
    public Exception? ExceptionToThrow { get; set; }

    /// <summary>Callback que se ejecuta al registrar la revocación, antes de lanzar la excepción.</summary>
    public Action<Guid, string>? OnRevoke { get; set; }

    public Task<int> RevokeAllAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del usuario no puede estar vacío.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Calls.Add((userId, reason));
        OnRevoke?.Invoke(userId, reason);

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return Task.FromResult(1);
    }
}
