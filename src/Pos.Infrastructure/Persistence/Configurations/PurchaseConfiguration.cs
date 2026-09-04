using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Persistence.Configurations;

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("Purchases");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.SupplierId)
            .IsRequired();

        builder.Property(p => p.OrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.ComplexProperty(p => p.TotalAmount, amountBuilder =>
        {
            amountBuilder.Property(m => m.Amount)
                .HasColumnName("TotalAmount")
                .HasColumnType("decimal(18,4)")
                .IsRequired();

            amountBuilder.Property(m => m.Currency)
                .HasColumnName("TotalCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(p => p.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired();

        // Mapeo OwnsMany para las líneas de detalle de compra en cascada
        builder.OwnsMany(p => p.LineItems, lineBuilder =>
        {
            lineBuilder.ToTable("PurchaseLineItems");

            lineBuilder.HasKey(l => l.Id);

            lineBuilder.Property(l => l.Id)
                .ValueGeneratedNever();

            lineBuilder.Property(l => l.ProductId)
                .IsRequired();

            lineBuilder.Property(l => l.ProductName)
                .IsRequired()
                .HasMaxLength(150);

            lineBuilder.Property(l => l.Quantity)
                .HasColumnType("decimal(18,4)")
                .IsRequired();

            lineBuilder.OwnsOne(l => l.UnitPrice, priceBuilder =>
            {
                priceBuilder.Property(m => m.Amount)
                    .HasColumnName("UnitPriceAmount")
                    .HasColumnType("decimal(18,4)")
                    .IsRequired();

                priceBuilder.Property(m => m.Currency)
                    .HasColumnName("UnitPriceCurrency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

            lineBuilder.OwnsOne(l => l.SubTotal, subTotalBuilder =>
            {
                subTotalBuilder.Property(m => m.Amount)
                    .HasColumnName("LineSubTotalAmount")
                    .HasColumnType("decimal(18,4)")
                    .IsRequired();

                subTotalBuilder.Property(m => m.Currency)
                    .HasColumnName("LineSubTotalCurrency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

            lineBuilder.WithOwner().HasForeignKey("PurchaseId");
        });

        builder.HasIndex(p => p.OrderNumber)
            .IsUnique();
        builder.HasIndex(p => p.SupplierId);
    }
}
