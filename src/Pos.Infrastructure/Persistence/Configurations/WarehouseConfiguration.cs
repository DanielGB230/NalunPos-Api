using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Persistence.Configurations;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(w => w.Description)
            .HasMaxLength(500);

        builder.Property(w => w.IsDefault)
            .IsRequired();

        builder.Property(w => w.IsActive)
            .IsRequired();

        builder.HasIndex(w => new { w.TenantId, w.Name })
            .IsUnique();

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(w => w.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
