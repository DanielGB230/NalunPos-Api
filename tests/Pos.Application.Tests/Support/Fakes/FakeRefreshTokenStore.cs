namespace Pos.Application.Tests.Support.Fakes;

using System;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

using Pos.Application.Common.Interfaces;
using Pos.Domain.Authentication;

/// <summary>
/// Falsa de <see cref="IRefreshTokenStore"/> para pruebas unitarias de Application.
/// Almacena en memoria los tokens emitidos y permite consultar por hash.
/// </summary>
public sealed class FakeRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, RefreshTokenEntity> _byHash = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, List<RefreshTokenEntity>> _byFamily = new();

    public Task<RefreshTokenCandidate?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        if (_byHash.TryGetValue(tokenHash, out var entity))
        {
            return Task.FromResult<RefreshTokenCandidate?>(new RefreshTokenCandidate(
                entity.TokenHash,
                entity.UserId,
                entity.FamilyId,
                entity.RevokedAtUtc is not null,
                entity.RevokedAtUtc,
                entity.ExpiresAtUtc));
        }
        return Task.FromResult<RefreshTokenCandidate?>(null);
    }

    public Task IssueAsync(Guid userId, Guid familyId, string tokenHash, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        var entity = new RefreshTokenEntity(Guid.NewGuid(), userId, tokenHash, familyId, DateTime.UtcNow, DateTime.UtcNow.Add(lifetime));
        _byHash[tokenHash] = entity;
        if (!_byFamily.TryGetValue(familyId, out var list))
        {
            list = [];
            _byFamily[familyId] = list;
        }
        list.Add(entity);
        return Task.CompletedTask;
    }

    public Task RevokeAsync(string tokenHash, string reason, DateTime revokedAtUtc, CancellationToken cancellationToken = default)
    {
        if (_byHash.TryGetValue(tokenHash, out var entity))
        {
            entity.Revoke(reason, revokedAtUtc);
        }
        return Task.CompletedTask;
    }

    public Task RevokeFamilyAsync(Guid familyId, string reason, DateTime revokedAtUtc, CancellationToken cancellationToken = default)
    {
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
        if (!_byHash.TryGetValue(currentTokenHash, out var current))
        {
            return Task.FromResult(new RefreshTokenRotationOutcome(false, false, null, null, null));
        }

        if (current.RevokedAtUtc is not null)
        {
            return Task.FromResult(new RefreshTokenRotationOutcome(false, true, null, null, null));
        }

        current.Revoke(RefreshTokenRevocationReasons.Rotated, nowUtc);
        current.ReplacedByTokenHash = newTokenHash;

        var entity = new RefreshTokenEntity(Guid.NewGuid(), current.UserId, newTokenHash, current.FamilyId, nowUtc, nowUtc.Add(lifetime));
        _byHash[newTokenHash] = entity;
        if (!_byFamily.TryGetValue(current.FamilyId, out var familyList))
        {
            familyList = [];
            _byFamily[current.FamilyId] = familyList;
        }
        familyList.Add(entity);

        return Task.FromResult(new RefreshTokenRotationOutcome(
            true, false, newTokenHash, current.UserId, current.FamilyId));
    }

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
        public DateTime? RevokedAtUtc { get; private set; }

        public RefreshTokenEntity(Guid id, Guid userId, string tokenHash, Guid familyId, DateTime createdAtUtc, DateTime expiresAtUtc)
        {
            Id = id;
            UserId = userId;
            TokenHash = tokenHash;
            FamilyId = familyId;
            CreatedAtUtc = createdAtUtc;
            ExpiresAtUtc = expiresAtUtc;
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