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
///      tiene <c>TenantId == null</c>. La tabla no está bajo RLS; el acceso se gobierna
///      por <see cref="TokenHash"/> y <see cref="FamilyId"/> mediante
///      <c>IAuthenticationSessionScope</c> (ADR 0030).
///   5. La familia tiene vigencia absoluta (<see cref="FamilyExpiresAtUtc"/>): el primer
///      token la fija y todos los rotados la heredan sin recalcularla. La expiración
///      deslizante de cada token nunca supera la vigencia absoluta (ADR 0033).
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
    public DateTime FamilyExpiresAtUtc { get; private set; }
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
        DateTime expiresAtUtc,
        DateTime familyExpiresAtUtc) : base(id)
    {
        if (createdAtUtc.Kind != DateTimeKind.Utc ||
            expiresAtUtc.Kind != DateTimeKind.Utc ||
            familyExpiresAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("Los instantes del refresh token deben ser UTC.");
        }

        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        FamilyExpiresAtUtc = familyExpiresAtUtc;
    }

    /// <summary>
    /// Crea el primer refresh token de una familia. Fija la vigencia absoluta de la
    /// familia (<c>creado + familyAbsoluteLifetime</c>) y calcula la expiración
    /// deslizante sin superarla.
    /// </summary>
    public static RefreshToken CreateInitial(
        Guid userId,
        string tokenHash,
        Guid familyId,
        TimeSpan slidingLifetime,
        TimeSpan familyAbsoluteLifetime,
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

        if (slidingLifetime <= TimeSpan.Zero)
        {
            throw new DomainException("La vigencia del refresh token debe ser positiva.");
        }

        if (familyAbsoluteLifetime <= TimeSpan.Zero)
        {
            throw new DomainException("La vigencia absoluta de la familia debe ser positiva.");
        }

        DateTime createdAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        DateTime familyExpiresAtUtc = createdAtUtc.Add(familyAbsoluteLifetime);
        DateTime expiresAtUtc = Earliest(createdAtUtc.Add(slidingLifetime), familyExpiresAtUtc);

        return new RefreshToken(
            Guid.CreateVersion7(),
            userId,
            tokenHash,
            familyId,
            createdAtUtc,
            expiresAtUtc,
            familyExpiresAtUtc);
    }

    /// <summary>
    /// Crea un refresh token rotado dentro de la familia del token actual. Hereda
    /// <see cref="FamilyExpiresAtUtc"/> sin recalcularla y recorta la expiración
    /// deslizante al tiempo restante de la familia.
    /// </summary>
    public static RefreshToken CreateRotated(
        RefreshToken currentToken,
        string tokenHash,
        TimeSpan slidingLifetime,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(currentToken);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (slidingLifetime <= TimeSpan.Zero)
        {
            throw new DomainException("La vigencia del refresh token debe ser positiva.");
        }

        DateTime createdAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        DateTime expiresAtUtc = Earliest(
            createdAtUtc.Add(slidingLifetime),
            currentToken.FamilyExpiresAtUtc);

        return new RefreshToken(
            Guid.CreateVersion7(),
            currentToken.UserId,
            tokenHash,
            currentToken.FamilyId,
            createdAtUtc,
            expiresAtUtc,
            currentToken.FamilyExpiresAtUtc);
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

    private static DateTime Earliest(DateTime left, DateTime right) =>
        left <= right ? left : right;
}
