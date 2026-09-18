using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Persistence.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.OrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Notes)
            .HasMaxLength(1000);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.HasIndex(p => new { p.TenantId, p.OrderNumber })
            .IsUnique();

        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(p => p.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(p => p.Lines, lineBuilder =>
        {
            lineBuilder.ToTable("PurchaseOrderLines");

            lineBuilder.WithOwner().HasForeignKey("PurchaseOrderId");
            lineBuilder.HasKey("Id");

            lineBuilder.Property(l => l.QuantityOrdered)
                .HasPrecision(18, 4)
                .IsRequired();

            lineBuilder.Property(l => l.QuantityReceived)
                .HasPrecision(18, 4)
                .IsRequired();

            lineBuilder.OwnsOne(l => l.UnitCost, costBuilder =>
            {
                costBuilder.Property(m => m.Amount)
                    .HasColumnName("UnitCostAmount")
                    .HasPrecision(18, 4)
                    .IsRequired();

                costBuilder.Property(m => m.Currency)
                    .HasColumnName("UnitCostCurrency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

            lineBuilder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(l => l.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
