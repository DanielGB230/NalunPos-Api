using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Persistence.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(s => s.ContactName)
            .HasMaxLength(100);

        builder.Property(s => s.Email)
            .HasMaxLength(150);

        builder.Property(s => s.Phone)
            .HasMaxLength(50);

        // Value Object TaxId usando ComplexProperty nativo de .NET 10
        builder.ComplexProperty(s => s.TaxId, taxBuilder =>
        {
            taxBuilder.Property(t => t.Value)
                .HasColumnName("TaxId")
                .HasMaxLength(25)
                .IsRequired();

            taxBuilder.Property(t => t.CountryCode)
                .HasColumnName("TaxCountryCode")
                .HasMaxLength(2)
                .IsRequired();
        });

        // Value Object Address usando ComplexProperty nativo de .NET 10
        builder.ComplexProperty(s => s.Address, addrBuilder =>
        {
            addrBuilder.Property(a => a.Street)
                .HasColumnName("Street")
                .HasMaxLength(200)
                .IsRequired();

            addrBuilder.Property(a => a.City)
                .HasColumnName("City")
                .HasMaxLength(100)
                .IsRequired();

            addrBuilder.Property(a => a.ZipCode)
                .HasColumnName("ZipCode")
                .HasMaxLength(20);

            addrBuilder.Property(a => a.Country)
                .HasColumnName("Country")
                .HasMaxLength(100)
                .IsRequired();
        });

        builder.Property(s => s.IsActive)
            .IsRequired();

        builder.Property(s => s.CreatedAtUtc)
            .IsRequired();

        builder.Property(s => s.UpdatedAtUtc);
    }
}
