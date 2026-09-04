using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedNever();

        builder.Property(a => a.TableName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.RecordId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Action)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(a => a.UserId);

        builder.Property(a => a.TimestampUtc)
            .IsRequired();

        builder.Property(a => a.OldValues);

        builder.Property(a => a.NewValues);

        builder.HasIndex(a => a.TableName);
        builder.HasIndex(a => a.RecordId);
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.TimestampUtc);
    }
}
