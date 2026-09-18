using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Persistence.Configurations;

public class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.ToTable("StockTransfers");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Notes)
            .HasMaxLength(1000);

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(t => t.SourceWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(t => t.DestinationWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(t => t.Lines, lineBuilder =>
        {
            lineBuilder.ToTable("StockTransferLines");

            lineBuilder.WithOwner().HasForeignKey("StockTransferId");
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
