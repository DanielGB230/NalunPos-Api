using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Persistence.Configurations;

public class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.ToTable("StockAdjustments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Reason)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(a => a.Notes)
            .HasMaxLength(1000);

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(a => a.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(a => a.Lines, lineBuilder =>
        {
            lineBuilder.ToTable("StockAdjustmentLines");

            lineBuilder.WithOwner().HasForeignKey("StockAdjustmentId");
            lineBuilder.HasKey("Id");

            lineBuilder.Property(l => l.Quantity)
                .HasPrecision(18, 4)
                .IsRequired();

            lineBuilder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(l => l.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
