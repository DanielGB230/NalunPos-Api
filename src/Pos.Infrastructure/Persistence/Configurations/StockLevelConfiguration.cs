using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Persistence.Configurations;

public class StockLevelConfiguration : IEntityTypeConfiguration<StockLevel>
{
    public void Configure(EntityTypeBuilder<StockLevel> builder)
    {
        builder.ToTable("StockLevels");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.QuantityAvailable)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(s => s.QuantityReserved)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(s => s.MinStockThreshold)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(s => s.RowVersion)
            .IsRowVersion()
            .HasDefaultValue(new byte[] { 0 });

        // Índice único compuesto para evitar duplicados del mismo producto en la misma ubicación
        builder.HasIndex(s => new { s.ProductId, s.WarehouseId, s.ContainerId })
            .IsUnique();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(s => s.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(s => s.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Container>()
            .WithMany()
            .HasForeignKey(s => s.ContainerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
