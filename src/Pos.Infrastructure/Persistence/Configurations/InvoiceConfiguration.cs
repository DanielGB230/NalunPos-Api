using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .ValueGeneratedNever();

        builder.Property(i => i.SaleId)
            .IsRequired();

        builder.Property(i => i.DocumentType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.DocumentNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.ComplexProperty(i => i.CustomerTaxId, taxBuilder =>
        {
            taxBuilder.Property(t => t.Value)
                .HasColumnName("CustomerTaxId")
                .HasMaxLength(25)
                .IsRequired();

            taxBuilder.Property(t => t.CountryCode)
                .HasColumnName("CustomerTaxCountryCode")
                .HasMaxLength(2)
                .IsRequired();
        });

        builder.ComplexProperty(i => i.TotalAmount, amountBuilder =>
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

        builder.Property(i => i.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.IssueDateUtc)
            .IsRequired();

        builder.HasIndex(i => i.DocumentNumber)
            .IsUnique();
        builder.HasIndex(i => i.SaleId);
    }
}
