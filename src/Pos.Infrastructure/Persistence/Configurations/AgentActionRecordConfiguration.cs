using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Entities;
using Pos.Domain.Enums;

namespace Pos.Infrastructure.Persistence.Configurations;

public class AgentActionRecordConfiguration : IEntityTypeConfiguration<AgentActionRecord>
{
    public void Configure(EntityTypeBuilder<AgentActionRecord> builder)
    {
        builder.ToTable("AgentActionRecords");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedNever();

        builder.Property(a => a.AgentId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.ProposedActionType)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(a => a.PayloadJson)
            .IsRequired();

        builder.Property(a => a.RiskLevel)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(a => a.ReviewedByUserId);

        builder.Property(a => a.CreatedAtUtc)
            .IsRequired();

        builder.Property(a => a.ReviewedAtUtc);

        builder.HasIndex(a => a.AgentId);
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.CreatedAtUtc);
    }
}
