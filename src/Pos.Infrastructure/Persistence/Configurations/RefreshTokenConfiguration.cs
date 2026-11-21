using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Authentication;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Fluent API para <see cref="RefreshToken"/>.
/// No es <see cref="Pos.Domain.Common.ITenantOwnedEntity"/> (no tiene TenantId):
/// el aislamiento se gobierna por UserId y la rotación por FamilyId.
/// </summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.UserId)
            .IsRequired();

        builder.Property(r => r.TokenHash)
            .IsRequired()
            .HasMaxLength(64); // SHA-256 hex = 64 caracteres

        builder.Property(r => r.FamilyId)
            .IsRequired();

        builder.Property(r => r.ReplacedByTokenHash)
            .HasMaxLength(64);

        builder.Property(r => r.RevokedReason)
            .HasMaxLength(100);

        // Las propiedades *Utc son gestionadas por UtcDateTimeConvention (Kind=Utc).
        builder.Property(r => r.CreatedAtUtc).IsRequired();
        builder.Property(r => r.ExpiresAtUtc).IsRequired();
        builder.Property(r => r.FamilyExpiresAtUtc).IsRequired();
        builder.Property(r => r.RevokedAtUtc);

        // Índices exigidos: TokenHash único; (UserId, FamilyId) único solo entre
        // los activos (filtro RevokedAtUtc IS NULL) para permitir rotación en
        // la misma familia sin violar unicidad del token anterior ya revocado.
        builder.HasIndex(r => r.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_RefreshTokens_TokenHash");

        builder.HasIndex(r => new { r.UserId, r.FamilyId })
            .IsUnique()
            .HasFilter("[RevokedAtUtc] IS NULL")
            .HasDatabaseName("IX_RefreshTokens_UserId_FamilyId_Active");

        builder.HasIndex(r => r.ExpiresAtUtc)
            .HasDatabaseName("IX_RefreshTokens_ExpiresAtUtc");

        // Índice filtrado que sostiene la purga por RevokedAtUtc (F1.G.6 / ADR 0033).
        builder.HasIndex(r => r.RevokedAtUtc)
            .HasFilter("[RevokedAtUtc] IS NOT NULL")
            .HasDatabaseName("IX_RefreshTokens_RevokedAtUtc");

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("IX_RefreshTokens_UserId");
    }
}
