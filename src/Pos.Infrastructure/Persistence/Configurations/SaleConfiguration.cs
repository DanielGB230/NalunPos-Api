using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Persistence.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.ReceiptNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.SessionId)
            .IsRequired();

        builder.Property(s => s.CustomerId);

        builder.ComplexProperty(s => s.SubTotal, amountBuilder =>
        {
            amountBuilder.Property(m => m.Amount)
                .HasColumnName("SubTotalAmount")
                .HasColumnType("decimal(18,4)")
                .IsRequired();

            amountBuilder.Property(m => m.Currency)
                .HasColumnName("SubTotalCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.ComplexProperty(s => s.TaxAmount, amountBuilder =>
        {
            amountBuilder.Property(m => m.Amount)
                .HasColumnName("TaxAmount")
                .HasColumnType("decimal(18,4)")
                .IsRequired();

            amountBuilder.Property(m => m.Currency)
                .HasColumnName("TaxCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.ComplexProperty(s => s.Total, amountBuilder =>
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

        builder.Property(s => s.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.CreatedAtUtc)
            .IsRequired();

        // Mapeo OwnsMany para las líneas de venta en cascada (SaleLineItem)
        builder.OwnsMany(s => s.LineItems, lineBuilder =>
        {
            lineBuilder.ToTable("SaleLineItems");

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

            lineBuilder.WithOwner().HasForeignKey("SaleId");
        });

        builder.HasIndex(s => s.ReceiptNumber)
            .IsUnique();
        builder.HasIndex(s => s.SessionId);
        builder.HasIndex(s => s.CustomerId);
    }
}
