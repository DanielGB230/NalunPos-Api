using Pos.Domain.Common;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Authentication;

/// <summary>
/// Refresh token rotativo de una sesión de autenticación.
///
/// Invariantes:
///   1. Solo se persiste el hash SHA-256 del token opaco (<see cref="TokenHash"/>);
///      el token en claro nunca se almacena.
///   2. Todos los instantes son UTC con <c>Kind=Utc</c> y se obtienen de <see cref="TimeProvider"/>
///      (ADR 0018 y ADR 0026).
///   3. La rotación se agrupa por <see cref="FamilyId"/>: cada token reemplazado deja
///      <see cref="ReplacedByTokenHash"/> y la familia completa se revoca ante reuso.
///   4. No implementa <c>ITenantOwnedEntity</c>: la identidad de plataforma (SuperAdmin)
///      tiene <c>TenantId == null</c> y el aislamiento se gobierna por <c>UserId</c> bajo RLS.
/// </summary>
public class RefreshToken : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public Guid FamilyId { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public string? RevokedReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    private RefreshToken()
    {
    }

    private RefreshToken(
        Guid id,
        Guid userId,
        string tokenHash,
        Guid familyId,
        DateTime createdAtUtc,
        DateTime expiresAtUtc) : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>
    /// Crea un refresh token activo con expiración calculada una sola vez desde
    /// <see cref="TimeProvider"/> (fuente única de tiempo).
    /// </summary>
    public static RefreshToken Create(
        Guid userId,
        string tokenHash,
        Guid familyId,
        TimeSpan lifetime,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (userId == Guid.Empty)
        {
            throw new DomainException("El UserId del refresh token no puede estar vacío.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (familyId == Guid.Empty)
        {
            throw new DomainException("El FamilyId del refresh token no puede estar vacío.");
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new DomainException("La vigencia del refresh token debe ser positiva.");
        }

        DateTime createdAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        DateTime expiresAtUtc = createdAtUtc.Add(lifetime);

        return new RefreshToken(
            Guid.CreateVersion7(),
            userId,
            tokenHash,
            familyId,
            createdAtUtc,
            expiresAtUtc);
    }

    /// <summary>
    /// Un token está activo mientras no haya sido revocado y su instante de expiración
    /// sea estrictamente posterior al instante UTC recibido.
    /// </summary>
    public bool IsActive(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("El instante de comparación del refresh token debe ser UTC.");
        }

        return RevokedAtUtc is null && utcNow < ExpiresAtUtc;
    }

    /// <summary>
    /// Revoca el token una única vez. Las revocaciones posteriores son idempotentes
    /// y no alteran el motivo original ni el reemplazo registrado.
    /// </summary>
    public void Revoke(string reason, TimeProvider timeProvider, string? replacedByTokenHash = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (RevokedAtUtc is not null)
        {
            return;
        }

        RevokedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        RevokedReason = reason;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
