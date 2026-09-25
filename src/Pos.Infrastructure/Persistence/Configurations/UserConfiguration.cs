using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;

namespace Pos.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", t => t.HasCheckConstraint(
            "CK_Users_PlatformRole",
            "[TenantId] IS NOT NULL OR [RoleId] = '00000000-0000-0000-0000-000000000001'"));

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .ValueGeneratedNever();

        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Email)
            .HasConversion(
                email => email.Value,
                value => new Email(value))
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.PasswordHash)
            .HasConversion(
                hash => hash.Value,
                value => new PasswordHash(value))
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.RoleId)
            .IsRequired();

        builder.HasOne<Role>()
            .WithMany()
            .HasPrincipalKey(r => new { r.TenantId, r.Id })
            .HasForeignKey(u => new { u.TenantId, u.RoleId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(u => u.TenantId);

        builder.Property(u => u.IsActive)
            .IsRequired();

        builder.Property(u => u.CreatedAtUtc)
            .IsRequired();

        builder.Property(u => u.UpdatedAtUtc);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.HasIndex(u => u.TenantId);
    }
}
