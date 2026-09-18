using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;

namespace Pos.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        // Value Object SKU usando ComplexProperty nativo de EF Core 8+ / .NET 10
        builder.ComplexProperty(p => p.Sku, skuBuilder =>
        {
            skuBuilder.Property(s => s.Value)
                .HasColumnName("Sku")
                .HasMaxLength(30)
                .IsRequired();
        });

        // Value Object Price (Money) usando ComplexProperty nativo de .NET 10
        builder.ComplexProperty(p => p.Price, priceBuilder =>
        {
            priceBuilder.Property(m => m.Amount)
                .HasColumnName("PriceAmount")
                .HasColumnType("decimal(18,4)")
                .IsRequired();

            priceBuilder.Property(m => m.Currency)
                .HasColumnName("PriceCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        // Value Object opcional Barcode mapeado con Value Converter fuertemente tipado para nulos
        var barcodeConverter = new ValueConverter<Barcode?, string?>(
            b => b != null ? b.Value : null,
            s => !string.IsNullOrEmpty(s) ? Barcode.Create(s) : null);

        builder.Property(p => p.Barcode)
            .HasConversion(barcodeConverter)
            .HasColumnName("Barcode")
            .HasMaxLength(50);

        // Value Object opcional Cost (Money) mapeado
        builder.OwnsOne(p => p.Cost, costBuilder =>
        {
            costBuilder.Property(m => m.Amount)
                .HasColumnName("CostAmount")
                .HasColumnType("decimal(18,4)");

            costBuilder.Property(m => m.Currency)
                .HasColumnName("CostCurrency")
                .HasMaxLength(3);
        });

        builder.Property(p => p.CategoryId)
            .IsRequired();

        builder.Property(p => p.IsActive)
            .IsRequired();

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired();

        builder.Property(p => p.UpdatedAtUtc);

        // Relación con Category
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
