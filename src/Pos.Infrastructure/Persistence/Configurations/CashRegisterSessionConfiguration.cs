using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Persistence.Configurations;

public class CashRegisterSessionConfiguration : IEntityTypeConfiguration<CashRegisterSession>
{
    public void Configure(EntityTypeBuilder<CashRegisterSession> builder)
    {
        builder.ToTable("CashRegisterSessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.CashRegisterId)
            .IsRequired();

        builder.Property(s => s.UserId)
            .IsRequired();

        builder.Property(s => s.OpenedAtUtc)
            .IsRequired();

        builder.Property(s => s.ClosedAtUtc);

        builder.ComplexProperty(s => s.InitialAmount, amountBuilder =>
        {
            amountBuilder.Property(m => m.Amount)
                .HasColumnName("InitialAmount")
                .HasColumnType("decimal(18,4)")
                .IsRequired();

            amountBuilder.Property(m => m.Currency)
                .HasColumnName("InitialAmountCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.OwnsOne(s => s.ExpectedFinalAmount, amountBuilder =>
        {
            amountBuilder.Property(m => m.Amount)
                .HasColumnName("ExpectedFinalAmount")
                .HasColumnType("decimal(18,4)");

            amountBuilder.Property(m => m.Currency)
                .HasColumnName("ExpectedFinalAmountCurrency")
                .HasMaxLength(3);
        });

        builder.OwnsOne(s => s.ActualFinalAmount, amountBuilder =>
        {
            amountBuilder.Property(m => m.Amount)
                .HasColumnName("ActualFinalAmount")
                .HasColumnType("decimal(18,4)");

            amountBuilder.Property(m => m.Currency)
                .HasColumnName("ActualFinalAmountCurrency")
                .HasMaxLength(3);
        });

        builder.Property(s => s.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.Notes)
            .HasMaxLength(500);

        builder.HasIndex(s => s.CashRegisterId);
        builder.HasIndex(s => s.UserId);
    }
}
