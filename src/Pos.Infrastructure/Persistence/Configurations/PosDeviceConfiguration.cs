using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Persistence.Configurations;

public class PosDeviceConfiguration : IEntityTypeConfiguration<PosDevice>
{
    public void Configure(EntityTypeBuilder<PosDevice> builder)
    {
        builder.ToTable("PosDevices");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .ValueGeneratedNever();

        builder.Property(d => d.BranchId)
            .IsRequired();

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.SerialNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.IsActive)
            .IsRequired();

        builder.Property(d => d.LastPingUtc);

        builder.Property(d => d.CreatedAtUtc)
            .IsRequired();

        builder.Property(d => d.UpdatedAtUtc);

        builder.HasIndex(d => d.SerialNumber)
            .IsUnique();

        builder.HasIndex(d => d.BranchId);
    }
}
