using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.FullName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.Email)
            .HasMaxLength(150);

        builder.Property(c => c.Phone)
            .HasMaxLength(50);

        // Value Object TaxId usando ComplexProperty nativo de .NET 10
        builder.ComplexProperty(c => c.TaxId, taxBuilder =>
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

        // Value Object opcional Address
        builder.OwnsOne(c => c.Address, addrBuilder =>
        {
            addrBuilder.Property(a => a.Street)
                .HasColumnName("Street")
                .HasMaxLength(200);

            addrBuilder.Property(a => a.City)
                .HasColumnName("City")
                .HasMaxLength(100);

            addrBuilder.Property(a => a.ZipCode)
                .HasColumnName("ZipCode")
                .HasMaxLength(20);

            addrBuilder.Property(a => a.Country)
                .HasColumnName("Country")
                .HasMaxLength(100);
        });

        builder.Property(c => c.IsActive)
            .IsRequired();

        builder.Property(c => c.CreatedAtUtc)
            .IsRequired();

        builder.Property(c => c.UpdatedAtUtc);
    }
}
