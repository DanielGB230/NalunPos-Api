using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Persistence.Configurations;

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("InventoryMovements");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .ValueGeneratedNever();

        builder.Property(m => m.ProductId)
            .IsRequired();

        builder.Property(m => m.WarehouseId)
            .IsRequired();

        builder.Property(m => m.ContainerId);

        builder.Property(m => m.Quantity)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(m => m.MovementType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(m => m.ReferenceId);

        builder.Property(m => m.Notes)
            .HasMaxLength(500);

        builder.Property(m => m.BatchNumber)
            .HasMaxLength(100);

        builder.Property(m => m.ExpirationDate);

        builder.Property(m => m.OccurredAtUtc)
            .IsRequired();

        builder.Property(m => m.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(m => new { m.ProductId, m.WarehouseId });
        builder.HasIndex(m => m.MovementType);
        builder.HasIndex(m => m.OccurredAtUtc);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(m => m.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
