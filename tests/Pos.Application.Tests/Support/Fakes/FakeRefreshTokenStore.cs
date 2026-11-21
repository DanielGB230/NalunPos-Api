namespace Pos.Application.Tests.Support.Fakes;

using System;
using System.Collections.Concurrent;

using Pos.Application.Common.Interfaces;
using Pos.Domain.Authentication;

/// <summary>
/// Falsa de <see cref="IRefreshTokenStore"/> para pruebas unitarias de Application.
/// Almacena en memoria los tokens emitidos y permite consultar por hash.
/// Expone contadores de llamadas para verificar que el handler no consume ni revoca
/// tokens cuando debe denegar o tolerar una rotación concurrente.
/// </summary>
public sealed class FakeRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, RefreshTokenEntity> _byHash = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, List<RefreshTokenEntity>> _byFamily = new();
    private int _rotateCallCount;
    private int _revokeCallCount;
    private int _revokeFamilyCallCount;

    public int RotateCallCount => _rotateCallCount;

    public int RevokeCallCount => _revokeCallCount;

    public int RevokeFamilyCallCount => _revokeFamilyCallCount;

    public string? LastRevokeFamilyReason { get; private set; }

    public int TokenCount => _byHash.Count;

    /// <summary>Fuente de tiempo determinista para emitir/rotar en pruebas.</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>Permite forzar el vencimiento efectivo del próximo token emitido.</summary>
    public DateTime? NextIssuedExpiresAtUtc { get; set; }

    /// <summary>Permite forzar el vencimiento efectivo del próximo token rotado.</summary>
    public DateTime? NextRotatedExpiresAtUtc { get; set; }

    /// <summary>
    /// Permite simular una carrera perdida en <see cref="RotateAsync"/>: cuando tiene valor,
    /// la rotación devuelve <c>Success=false</c> con este estado actual del token.
    /// </summary>
    public RefreshTokenCandidate? SimulateRotationRace { get; set; }

    /// <summary>
    /// Inserta un token con estado determinista (sin depender del reloj del sistema).
    /// </summary>
    public void SeedToken(
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        string? revokedReason = null,
        DateTime? revokedAtUtc = null,
        DateTime? familyExpiresAtUtc = null)
    {
        var entity = new RefreshTokenEntity(
            Guid.NewGuid(),
            userId,
            tokenHash,
            familyId,
            createdAtUtc,
            expiresAtUtc,
            familyExpiresAtUtc ?? expiresAtUtc);
        if (revokedAtUtc is not null)
        {
            entity.Revoke(revokedReason ?? throw new ArgumentNullException(nameof(revokedReason)), revokedAtUtc.Value);
        }

        _byHash[tokenHash] = entity;
        if (!_byFamily.TryGetValue(familyId, out var list))
        {
            list = [];
            _byFamily[familyId] = list;
        }
        list.Add(entity);
    }

    public string? GetRevokedReason(string tokenHash) =>
        _byHash.TryGetValue(tokenHash, out var entity) ? entity.RevokedReason : null;

    public Task<RefreshTokenCandidate?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        if (_byHash.TryGetValue(tokenHash, out var entity))
        {
            return Task.FromResult<RefreshTokenCandidate?>(ToCandidate(entity));
        }
        return Task.FromResult<RefreshTokenCandidate?>(null);
    }

    public Task<DateTime> IssueAsync(
        Guid userId,
        Guid familyId,
        string tokenHash,
        TimeSpan slidingLifetime,
        TimeSpan familyAbsoluteLifetime,
        CancellationToken cancellationToken = default)
    {
        DateTime nowUtc = TimeProvider.GetUtcNow().UtcDateTime;
        DateTime familyExpiresAtUtc = nowUtc.Add(familyAbsoluteLifetime);
        DateTime expiresAtUtc = NextIssuedExpiresAtUtc ?? Min(nowUtc.Add(slidingLifetime), familyExpiresAtUtc);
        NextIssuedExpiresAtUtc = null;

        var entity = new RefreshTokenEntity(
            Guid.NewGuid(),
            userId,
            tokenHash,
            familyId,
            nowUtc,
            expiresAtUtc,
            familyExpiresAtUtc);

        _byHash[tokenHash] = entity;
        if (!_byFamily.TryGetValue(familyId, out var list))
        {
            list = [];
            _byFamily[familyId] = list;
        }
        list.Add(entity);
        return Task.FromResult(expiresAtUtc);
    }

    public Task RevokeAsync(string tokenHash, string reason, DateTime revokedAtUtc, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _revokeCallCount);
        if (_byHash.TryGetValue(tokenHash, out var entity))
        {
            entity.Revoke(reason, revokedAtUtc);
        }
        return Task.CompletedTask;
    }

    public Task RevokeFamilyAsync(Guid familyId, string reason, DateTime revokedAtUtc, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _revokeFamilyCallCount);
        LastRevokeFamilyReason = reason;
        if (_byFamily.TryGetValue(familyId, out var list))
        {
            foreach (var entity in list.Where(e => e.RevokedAtUtc is null))
            {
                entity.Revoke(reason, revokedAtUtc);
            }
        }
        return Task.CompletedTask;
    }

    public Task<RefreshTokenRotationOutcome> RotateAsync(
        string currentTokenHash,
        string newTokenHash,
        DateTime nowUtc,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _rotateCallCount);

        if (SimulateRotationRace is not null)
        {
            return Task.FromResult(new RefreshTokenRotationOutcome(
                Success: false,
                CurrentToken: SimulateRotationRace,
                NewTokenHash: null,
                UserId: null,
                FamilyId: null));
        }

        if (!_byHash.TryGetValue(currentTokenHash, out var current))
        {
            return Task.FromResult(new RefreshTokenRotationOutcome(false, null, null, null, null));
        }

        if (current.RevokedAtUtc is not null)
        {
            return Task.FromResult(new RefreshTokenRotationOutcome(false, ToCandidate(current), null, null, null));
        }

        current.Revoke(RefreshTokenRevocationReasons.Rotated, nowUtc);
        current.ReplacedByTokenHash = newTokenHash;

        DateTime familyExpiresAtUtc = current.FamilyExpiresAtUtc;
        DateTime expiresAtUtc = NextRotatedExpiresAtUtc ?? Min(nowUtc.Add(lifetime), familyExpiresAtUtc);
        NextRotatedExpiresAtUtc = null;

        var entity = new RefreshTokenEntity(
            Guid.NewGuid(),
            current.UserId,
            newTokenHash,
            current.FamilyId,
            nowUtc,
            expiresAtUtc,
            familyExpiresAtUtc);

        _byHash[newTokenHash] = entity;
        if (!_byFamily.TryGetValue(current.FamilyId, out var familyList))
        {
            familyList = [];
            _byFamily[current.FamilyId] = familyList;
        }
        familyList.Add(entity);

        return Task.FromResult(new RefreshTokenRotationOutcome(
            true, null, newTokenHash, current.UserId, current.FamilyId, expiresAtUtc));
    }

    private static DateTime Min(DateTime left, DateTime right) => left <= right ? left : right;

    private static RefreshTokenCandidate ToCandidate(RefreshTokenEntity entity) =>
        new(
            entity.TokenHash,
            entity.UserId,
            entity.FamilyId,
            entity.RevokedAtUtc is not null,
            entity.RevokedAtUtc,
            entity.ExpiresAtUtc,
            entity.RevokedReason);

    private sealed class RefreshTokenEntity
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid UserId { get; }
        public string TokenHash { get; }
        public Guid FamilyId { get; }
        public string? ReplacedByTokenHash { get; set; }
        public string? RevokedReason { get; private set; }
        public DateTime CreatedAtUtc { get; }
        public DateTime ExpiresAtUtc { get; }
        public DateTime FamilyExpiresAtUtc { get; }
        public DateTime? RevokedAtUtc { get; private set; }

        public RefreshTokenEntity(
            Guid id,
            Guid userId,
            string tokenHash,
            Guid familyId,
            DateTime createdAtUtc,
            DateTime expiresAtUtc,
            DateTime familyExpiresAtUtc)
        {
            Id = id;
            UserId = userId;
            TokenHash = tokenHash;
            FamilyId = familyId;
            CreatedAtUtc = createdAtUtc;
            ExpiresAtUtc = expiresAtUtc;
            FamilyExpiresAtUtc = familyExpiresAtUtc;
        }

        public void Revoke(string reason, DateTime revokedAtUtc)
        {
            if (RevokedAtUtc is null)
            {
                RevokedAtUtc = revokedAtUtc;
                RevokedReason = reason;
            }
        }
    }
}

/// <summary>
/// Generador falso de refresh tokens para pruebas unitarias.
/// Devuelve un token predecible y su hash SHA-256.
/// </summary>
public sealed class FakeRefreshTokenGenerator : IRefreshTokenGenerator
{
    public static readonly string FixedToken = "fake-refresh-token-0123456789abcdef";
    public static readonly string FixedTokenHash = ComputeSha256(FixedToken);

    public RefreshTokenMaterial Generate() => new(FixedToken, FixedTokenHash);

    private static string ComputeSha256(string token)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

/// <summary>
/// Falsa de <see cref="RefreshTokenOptions"/> para pruebas sin configuración real.
/// </summary>
public sealed class FakeRefreshTokenOptions : Pos.Application.Common.Options.RefreshTokenOptions
{
    public FakeRefreshTokenOptions()
    {
        RefreshTokenDays = 7;
        CookieName = "nalunpos_rt";
    }
}
