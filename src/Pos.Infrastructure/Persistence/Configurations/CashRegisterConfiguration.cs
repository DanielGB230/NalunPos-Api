using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Persistence.Configurations;

public class CashRegisterConfiguration : IEntityTypeConfiguration<CashRegister>
{
    public void Configure(EntityTypeBuilder<CashRegister> builder)
    {
        builder.ToTable("CashRegisters", t =>
        {
            t.HasCheckConstraint(
                "CK_CashRegisters_BranchId_NotEmpty",
                "[BranchId] <> '00000000-0000-0000-0000-000000000000'");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.BranchId)
            .IsRequired();

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.SerialNumber)
            .HasMaxLength(50);

        builder.Property(r => r.IsActive)
            .IsRequired();

        builder.Property(r => r.CurrentSessionId);

        builder.Property(r => r.CreatedAtUtc)
            .IsRequired();

        builder.Property(r => r.UpdatedAtUtc);

        builder.HasIndex(r => new { r.TenantId, r.BranchId });

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(r => r.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
