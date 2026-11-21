using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pos.Api.BackgroundServices;
using Pos.Api.Options;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Authentication;
using Pos.Infrastructure.Authentication;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

/// <summary>
/// Purga real de refresh tokens (F1.G.6) contra SQL Server:
/// solo elimina tokens fuera de la retención, por lotes, de forma idempotente,
/// y el BackgroundService respeta <c>Enabled=false</c>.
/// </summary>
[Collection("IntegrationTests")]
public class RefreshTokenPurgeSqlServerIntegrationTests
{
    private static readonly TimeSpan s_retention = TimeSpan.FromDays(7);

    private readonly MsSqlTestFixture _fixture;

    public RefreshTokenPurgeSqlServerIntegrationTests(MsSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Purge_DeletesOnlyTokensOutsideRetention_InMultipleBatches_AndIsIdempotent()
    {
        DateTime nowUtc = DateTime.UtcNow;
        var seeded = BuildSeedPlan(nowUtc);
        var hashes = seeded.Select(s => s.TokenHash).ToArray();

        try
        {
            await SeedAsync(seeded);

            int deleted;
            var sp = _fixture.CreateServiceProvider(
                configureServices: services => services.AddScoped<IRefreshTokenPurger, RefreshTokenPurger>());
            using (var scope = sp.CreateScope())
            {
                var purger = scope.ServiceProvider.GetRequiredService<IRefreshTokenPurger>();
                deleted = await purger.PurgeAsync(nowUtc, s_retention, batchSize: 2, CancellationToken.None);

                int secondRun = await purger.PurgeAsync(nowUtc, s_retention, batchSize: 2, CancellationToken.None);
                Assert.Equal(0, secondRun);
            }

            Assert.Equal(5, deleted);

            await using var db = _fixture.CreateDbContext(isSuperAdmin: true);
            var remaining = await db.RefreshTokens
                .AsNoTracking()
                .Where(r => hashes.Contains(r.TokenHash))
                .Select(r => r.TokenHash)
                .ToListAsync();

            Assert.Equal(3, remaining.Count);
            Assert.Contains(seeded.Single(s => s.Label == "active").TokenHash, remaining);
            Assert.Contains(seeded.Single(s => s.Label == "revoked-recent").TokenHash, remaining);
            Assert.Contains(seeded.Single(s => s.Label == "expired-recent").TokenHash, remaining);

            foreach (var eligible in seeded.Where(s => s.Eligible))
            {
                Assert.DoesNotContain(eligible.TokenHash, remaining);
            }
        }
        finally
        {
            await DeleteByHashesAsync(hashes);
        }
    }

    [Fact]
    public async Task BackgroundService_WhenDisabled_DoesNotSchedule_ButPurgeAsyncStillWorksWhenInvoked()
    {
        DateTime nowUtc = DateTime.UtcNow;
        var seeded = new List<SeededToken>
        {
            CreateSeed("disabled-eligible", nowUtc, createdAtAge: TimeSpan.FromDays(40), revokedAge: null, eligible: true)
        };
        var hashes = seeded.Select(s => s.TokenHash).ToArray();

        try
        {
            await SeedAsync(seeded);

            var sp = _fixture.CreateServiceProvider(
                configureServices: services => services.AddScoped<IRefreshTokenPurger, RefreshTokenPurger>());

            var settings = new RefreshTokenPurgeSettings
            {
                Enabled = false,
                IntervalMinutes = 60,
                RetentionDays = 7,
                BatchSize = 2
            };

            var service = new RefreshTokenPurgeBackgroundService(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<ILogger<RefreshTokenPurgeBackgroundService>>(),
                Options.Create(settings),
                new FixedTimeProvider(nowUtc));

            Task startTask = service.StartAsync(CancellationToken.None);
            Assert.True(
                startTask.IsCompleted,
                "Con Enabled=false el BackgroundService no debe programar ninguna espera.");

            await service.StopAsync(CancellationToken.None);

            await using (var db = _fixture.CreateDbContext(isSuperAdmin: true))
            {
                Assert.True(await db.RefreshTokens.AnyAsync(r => r.TokenHash == seeded[0].TokenHash));
            }

            int deleted = await service.PurgeAsync(CancellationToken.None);
            Assert.Equal(1, deleted);

            await using (var db = _fixture.CreateDbContext(isSuperAdmin: true))
            {
                Assert.False(await db.RefreshTokens.AnyAsync(r => r.TokenHash == seeded[0].TokenHash));
            }
        }
        finally
        {
            await DeleteByHashesAsync(hashes);
        }
    }

    private static List<SeededToken> BuildSeedPlan(DateTime nowUtc) =>
    [
        CreateSeed("active", nowUtc, createdAtAge: TimeSpan.FromDays(1), revokedAge: null, eligible: false),
        CreateSeed("revoked-recent", nowUtc, createdAtAge: TimeSpan.FromDays(10), revokedAge: TimeSpan.FromDays(1), eligible: false),
        CreateSeed("revoked-old-1", nowUtc, createdAtAge: TimeSpan.FromDays(40), revokedAge: TimeSpan.FromDays(30), eligible: true),
        CreateSeed("revoked-old-2", nowUtc, createdAtAge: TimeSpan.FromDays(50), revokedAge: TimeSpan.FromDays(40), eligible: true),
        CreateSeed("expired-old-1", nowUtc, createdAtAge: TimeSpan.FromDays(40), revokedAge: null, eligible: true),
        CreateSeed("expired-old-2", nowUtc, createdAtAge: TimeSpan.FromDays(60), revokedAge: null, eligible: true),
        CreateSeed("expired-old-3", nowUtc, createdAtAge: TimeSpan.FromDays(20), revokedAge: null, eligible: true),
        CreateSeed("expired-recent", nowUtc, createdAtAge: TimeSpan.FromDays(8), revokedAge: null, eligible: false)
    ];

    private static SeededToken CreateSeed(
        string label,
        DateTime nowUtc,
        TimeSpan createdAtAge,
        TimeSpan? revokedAge,
        bool eligible) =>
        new(label, NewHash(), createdAtAge, revokedAge, eligible);

    private async Task SeedAsync(IReadOnlyCollection<SeededToken> seeds)
    {
        DateTime nowUtc = DateTime.UtcNow;
        var tokens = seeds.Select(seed =>
        {
            DateTime createdAtUtc = nowUtc - seed.CreatedAtAge;
            var token = RefreshToken.CreateInitial(
                Guid.NewGuid(),
                seed.TokenHash,
                Guid.NewGuid(),
                slidingLifetime: TimeSpan.FromDays(7),
                familyAbsoluteLifetime: TimeSpan.FromDays(30),
                timeProvider: new FixedTimeProvider(createdAtUtc));

            if (seed.RevokedAge is not null)
            {
                token.Revoke(
                    RefreshTokenRevocationReasons.Logout,
                    new FixedTimeProvider(nowUtc - seed.RevokedAge.Value));
            }

            return token;
        }).ToList();

        await using var db = _fixture.CreateDbContext(isSuperAdmin: true);
        db.RefreshTokens.AddRange(tokens);
        await db.SaveChangesAsync();
    }

    private async Task DeleteByHashesAsync(IReadOnlyCollection<string> hashes)
    {
        await using var db = _fixture.CreateDbContext(isSuperAdmin: true);
        await db.RefreshTokens
            .Where(r => hashes.Contains(r.TokenHash))
            .ExecuteDeleteAsync();
    }

    private static string NewHash() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private sealed record SeededToken(
        string Label,
        string TokenHash,
        TimeSpan CreatedAtAge,
        TimeSpan? RevokedAge,
        bool Eligible);

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}
